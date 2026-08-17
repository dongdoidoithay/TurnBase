using System.Collections.Generic;
using Game.Combat.Ai;
using Game.Combat.Events;
using Game.Combat.Model;
using Game.Combat.Systems;
using Game.Core.Random;
using Game.Data;

namespace Game.Combat
{
    /// <summary>Một hành động do người chơi/AI quyết định. Cũng là đơn vị lưu của replay.</summary>
    public readonly struct ActionIntent
    {
        public readonly int ActorId;
        public readonly int SkillSlot;
        public readonly int TargetId;
        public readonly CommandGrade Grade;
        public readonly bool IsGuard;
        public readonly bool IsEscape;
        /// <summary>task-consumable-items.md — dùng vật phẩm thay vì skill. Tái dùng
        /// <see cref="SkillSlot"/> làm chỉ số <c>ItemType</c> (ép kiểu), không thêm field mới,
        /// cùng khuôn <see cref="IsGuard"/>/<see cref="IsEscape"/> đã có.</summary>
        public readonly bool IsUseItem;
        /// <summary>task-tactic-row.md — hoán đổi hàng Front/Back, KHÔNG kết thúc lượt.</summary>
        public readonly bool IsSwapRow;
        /// <summary>task-tactic-row.md — kết thúc lượt, đảm bảo chí mạng lượt kế tiếp.</summary>
        public readonly bool IsFocus;
        /// <summary>task-analyze-tactic.md — 5 SP, reveal stat địch (auto-target). Vĩnh viễn trong trận.</summary>
        public readonly bool IsAnalyze;

        public ActionIntent(int actorId, int skillSlot, int targetId,
                            CommandGrade grade = CommandGrade.Good,
                            bool isGuard = false, bool isEscape = false, bool isUseItem = false,
                            bool isSwapRow = false, bool isFocus = false, bool isAnalyze = false)
        {
            ActorId = actorId; SkillSlot = skillSlot; TargetId = targetId;
            Grade = grade; IsGuard = isGuard; IsEscape = isEscape; IsUseItem = isUseItem;
            IsSwapRow = isSwapRow; IsFocus = isFocus; IsAnalyze = isAnalyze;
        }
    }

    /// <summary>Trạng thái của máy trạng thái trận (plan.md §4.2).</summary>
    public enum SimPhase
    {
        Init, Intro, RoundStart, TurnStart, AwaitInput,
        Resolve, AfterEffects, TurnEnd, RoundEnd, Finished
    }

    /// <summary>
    /// Điểm vào của simulation. C# THUẦN — không Unity API, chạy được headless.
    /// Vòng đời: Start() → lặp { Advance() → nếu AwaitInput thì SubmitIntent() } → Result != InProgress.
    /// </summary>
    public sealed class CombatSimulation
    {
        public const int SAFETY_TURN_LIMIT = 200;   // edge case E13

        public BattleState State { get; }
        public CombatEventQueue Events { get; }
        public SimPhase Phase { get; private set; } = SimPhase.Init;

        private readonly TurnScheduler _scheduler;
        private readonly StatusProcessor _status;
        private readonly PassiveProcessor _passive;
        private readonly PoiseSystem _poise;
        private readonly TargetSelector _targeting;
        private readonly ActionResolver _resolver;
        private readonly ItemResolver _items;
        private readonly AIController _ai;

        private readonly Dictionary<int, AIProfile> _aiProfiles = new();
        private readonly List<ActionIntent> _intentLog = new(128);

        private CombatUnit _currentActor;
        /// <summary>task-setbonus.md §1.2 — bộ Tempest: đã phát cờ IsFirstActorThisRound cho round
        /// hiện tại chưa. Reset ở BeginRound(), set true lần đầu ở BeginTurn().</summary>
        private bool _roundFirstActorAssigned;

        /// <summary>Tháp Vô Tận (task-endgame.md, plan.md §8.3): gọi ngay khi phe Enemy bị wipe
        /// nhưng TRƯỚC KHI trận được chốt Victory — nếu trả về true (đã spawn đợt địch mới vào
        /// <see cref="BattleState.Units"/> qua <c>AddUnit</c>), trận tiếp tục chạy như chưa có gì
        /// xảy ra thay vì kết thúc. Null (mặc định) = hành vi cũ, không đổi cho mọi trận khác.</summary>
        public System.Func<CombatSimulation, bool> OnEnemyWaveCleared;

        public ulong Seed => State.Rng.Seed;
        public IReadOnlyList<ActionIntent> IntentLog => _intentLog;
        public CombatUnit CurrentActor => _currentActor;
        public bool IsFinished => State.Result != BattleResult.InProgress;
        public bool NeedsPlayerInput => Phase == SimPhase.AwaitInput
                                        && _currentActor != null
                                        && _currentActor.Side == TeamSide.Player;

        public CombatSimulation(ulong seed, int turnLimit = 0)
        {
            State = new BattleState
            {
                Rng = new XorShiftRandom(seed),
                TurnLimit = turnLimit
            };
            Events = new CombatEventQueue();

            _scheduler = new TurnScheduler(State);
            _status = new StatusProcessor(State, Events);
            _passive = new PassiveProcessor(State, Events, _status);
            _poise = new PoiseSystem(State, Events, _status, _passive);
            _targeting = new TargetSelector(State);
            _resolver = new ActionResolver(State, Events, _status, _passive, _poise, _targeting);
            _items = new ItemResolver(State, Events, _status, _resolver);
            _ai = new AIController(State, _targeting);
        }

        public void RegisterAi(int unitId, AIProfile profile) => _aiProfiles[unitId] = profile;

        public CombatUnit AddUnit(CombatUnit unit, AIProfile ai = null)
        {
            State.AddUnit(unit);
            unit.FillResources();
            if (ai != null) _aiProfiles[unit.Id] = ai;
            return unit;
        }

        // =====================================================================
        // VÒNG ĐỜI
        // =====================================================================

        public void Start()
        {
            Events.Emit(CombatEventType.BattleInitialized, intValue: State.Units.Count);
            Events.Emit(CombatEventType.BattleStarted);
            _passive.TriggerBattleStart(State.Rng);
            Phase = SimPhase.RoundStart;
        }

        /// <summary>
        /// Chạy tới khi cần input của người chơi hoặc trận kết thúc.
        /// Trả về true nếu đang chờ input.
        /// </summary>
        public bool Advance()
        {
            int guard = 0;

            while (!IsFinished)
            {
                if (++guard > 10_000) { Finish(BattleResult.Timeout); return false; }

                switch (Phase)
                {
                    case SimPhase.Init:
                        Start();
                        break;

                    case SimPhase.RoundStart:
                        BeginRound();
                        break;

                    case SimPhase.TurnStart:
                        if (!BeginTurn()) continue;   // lượt bị bỏ qua → sang unit kế
                        Phase = SimPhase.AwaitInput;
                        break;

                    case SimPhase.AwaitInput:
                        if (_currentActor.Side == TeamSide.Player) return true;   // chờ người chơi
                        RunAiTurn();
                        break;

                    case SimPhase.TurnEnd:
                        // FinishTurn() đã chạy xong phần dọn dẹp và tự chuyển phase;
                        // tới đây chỉ cần quay lại vòng lượt.
                        Phase = SimPhase.TurnStart;
                        break;

                    case SimPhase.RoundEnd:
                        Phase = SimPhase.RoundStart;
                        break;

                    default:
                        Phase = SimPhase.TurnStart;
                        break;
                }
            }
            return false;
        }

        /// <summary>Người chơi nộp hành động. Chỉ hợp lệ khi NeedsPlayerInput == true.</summary>
        public void SubmitIntent(in ActionIntent intent)
        {
            if (Phase != SimPhase.AwaitInput || _currentActor == null) return;
            if (intent.ActorId != _currentActor.Id) return;

            _intentLog.Add(intent);
            ExecuteIntent(intent);
        }

        // =====================================================================
        // PIPELINE MỘT LƯỢT — plan.md §4.3
        // =====================================================================

        private void BeginRound()
        {
            State.RoundNumber++;
            Events.Emit(CombatEventType.RoundStarted, intValue: State.RoundNumber);

            foreach (var kv in _aiProfiles) AIController.TickRuleCooldowns(kv.Value);

            // task-setbonus.md §1.2/§1.3 — bộ Tempest + round boundary thật: reset cả 2 cờ mỗi
            // round mới (IsFirstActorThisRound cho Tempest, HasActedThisRound để biết khi nào hết round).
            _roundFirstActorAssigned = false;
            for (int i = 0; i < State.Units.Count; i++)
            {
                State.Units[i].IsFirstActorThisRound = false;
                State.Units[i].HasActedThisRound = false;
            }

            RefreshIntents();
            Phase = SimPhase.TurnStart;
        }

        /// <summary>Bước 1–7. Trả về false nếu lượt bị bỏ qua (chết/mất lượt).</summary>
        private bool BeginTurn()
        {
            // Bước 1–2: tick ATB, chọn actor
            _currentActor = _scheduler.AdvanceToNextActor();
            if (_currentActor == null) { CheckEnd(); return false; }

            State.CurrentActorId = _currentActor.Id;
            State.TurnCounter++;
            // task-setbonus.md §1.3 — tính CẢ turn sắp bị skip (chết/stun/paralyze ở các nhánh
            // dưới) là "đã qua lượt round này", khác IsFirstActorThisRound (set riêng, chỉ khi
            // chắc chắn không skip) — 1 unit bị khoá hành động vĩnh viễn không được chặn round
            // kết thúc mãi mãi.
            _currentActor.HasActedThisRound = true;

            if (State.TurnCounter > SAFETY_TURN_LIMIT) { Finish(BattleResult.Timeout); return false; }

            Events.Emit(CombatEventType.TurnStarted, _currentActor.Id, intValue: State.TurnCounter);
            _passive.TriggerTurnStart(_currentActor, State.Rng);

            // Bước 3–4: DoT tick, chết do DoT thì bỏ lượt
            bool died = _status.TickTurnStart(_currentActor);
            if (died) { FinishTurnSkipped(); return false; }

            // Bước 5: control chặn hành động
            if (_currentActor.IsActionBlocked()) { FinishTurnSkipped(); return false; }

            // Paralyze: 50% mất lượt
            if (_currentActor.HasStatus(StatusId.Paralyze)
                && State.Rng.Chance(StatusTable.PARALYZE_SKIP_CHANCE))
            {
                FinishTurnSkipped();
                return false;
            }

            // Minion hết hạn (edge case E20)
            if (_currentActor.IsMinion)
            {
                var owner = _currentActor.OwnerId >= 0 ? State.GetUnit(_currentActor.OwnerId) : null;
                if (--_currentActor.MinionTurnsLeft <= 0 || owner == null || owner.IsDead)
                {
                    _currentActor.Hp = 0;
                    Events.Emit(CombatEventType.UnitDied, target: _currentActor.Id);
                    FinishTurnSkipped();
                    return false;
                }
            }

            // Bước 6: hồi SP
            int spBefore = _currentActor.Sp;
            _currentActor.AddSp((int)_currentActor.Stats.SpRegen);
            if (_currentActor.Sp != spBefore)
                Events.Emit(CombatEventType.SpChanged, _currentActor.Id, _currentActor.Id,
                            intValue: _currentActor.Sp);

            // Bước 7: giảm cooldown
            for (int i = 0; i < _currentActor.Skills.Count; i++)
            {
                var s = _currentActor.Skills[i];
                if (s.CooldownLeft > 0)
                {
                    s.CooldownLeft--;
                    Events.Emit(CombatEventType.CooldownChanged, _currentActor.Id,
                                intValue: s.CooldownLeft, stringValue: s.Data.Id);
                }
            }

            // task-setbonus.md §1.2 — bộ Tempest: actor CHẮC CHẮN sẽ hành động thật (mọi nhánh
            // skip ở trên đã return sớm) — nếu chưa ai được phát cờ round này, actor này là người
            // đầu tiên.
            if (!_roundFirstActorAssigned)
            {
                _currentActor.IsFirstActorThisRound = true;
                _roundFirstActorAssigned = true;
            }

            // task-tactic-row.md — reset cờ SwapRow mỗi lượt mới.
            _currentActor.HasSwappedRowThisTurn = false;

            return true;
        }

        private void RunAiTurn()
        {
            _aiProfiles.TryGetValue(_currentActor.Id, out var profile);
            var decision = _ai.Choose(_currentActor, profile, 0, State.Rng);

            if (!decision.IsValid) { FinishTurnSkipped(); return; }

            var intent = new ActionIntent(_currentActor.Id, decision.Skill.SlotIndex,
                                          decision.TargetId, CommandGrade.Good);
            _intentLog.Add(intent);
            ExecuteIntent(intent);
        }

        private void ExecuteIntent(in ActionIntent intent)
        {
            Phase = SimPhase.Resolve;

            if (intent.IsEscape)
            {
                if (State.AllowEscape && TryEscape()) { Finish(BattleResult.Escaped); return; }
                FinishTurn(0);
                return;
            }

            if (intent.IsGuard)
            {
                // Guard: +50% DEF tới lượt sau, hồi 8 SP
                _status.Apply(_currentActor, _currentActor,
                              new StatusApplication(StatusId.DefUp, 1f, 1, 2, true), State.Rng);
                _currentActor.AddSp(8);
                Events.Emit(CombatEventType.SpChanged, _currentActor.Id, _currentActor.Id,
                            intValue: _currentActor.Sp);
                FinishTurn(0);
                return;
            }

            if (intent.IsSwapRow)
            {
                // task-tactic-row.md — hoán đổi hàng, KHÔNG kết thúc lượt (actor vẫn chờ input).
                if (!_currentActor.HasSwappedRowThisTurn)
                {
                    _currentActor.Row = _currentActor.Row == Row.Front ? Row.Back : Row.Front;
                    _currentActor.HasSwappedRowThisTurn = true;
                }
                Phase = SimPhase.AwaitInput;
                return;
            }

            if (intent.IsFocus)
            {
                // task-tactic-row.md — đảm bảo chí mạng lượt kế. Duration=2: FinishTurn của turn này
                // tick từ 2→1, nên Focus còn 1 turn khi actor hành động lần kế tiếp.
                _status.Apply(_currentActor, _currentActor,
                              new StatusApplication(StatusId.Focus, 1f, 2, 1, true), State.Rng);
                FinishTurn(0);
                return;
            }

            if (intent.IsAnalyze)
            {
                // task-analyze-tactic.md — 5 SP để reveal stat địch. Nếu không đủ SP thì bỏ qua
                // (không để mất lượt vô lý, cùng quy ước skill không đủ SP fallback về BasicAttack).
                if (_currentActor.Sp >= 5)
                {
                    _currentActor.AddSp(-5);
                    Events.Emit(CombatEventType.SpChanged, _currentActor.Id, _currentActor.Id,
                                intValue: _currentActor.Sp);
                    var target = _targeting.AutoSuggest(_currentActor, TeamSide.Enemy);
                    if (target != null) State.AnalyzedEnemyIds.Add(target.Id);
                }
                FinishTurn(0);
                return;
            }

            if (intent.IsUseItem)
            {
                ExecuteUseItem(intent);
                return;
            }

            var skill = _currentActor.GetSkill(intent.SkillSlot);
            if (skill == null || !_currentActor.CanUseSkill(skill, State.IsUltimateReady))
            {
                // Không dùng được thì rơi về đánh thường (không để người chơi mất lượt vô lý)
                skill = _currentActor.GetSkill(0);
                if (skill == null) { FinishTurn(0); return; }
            }

            _resolver.Execute(_currentActor, skill, intent.TargetId, intent.Grade, State.Rng);

            // Edge case E15 (plan.md §4.14): tiêu gauge Ultimate CHỈ khi actor còn sống sau khi
            // resolve — actor tự chết giữa lúc dùng Ultimate (VD phản đòn Counter/Reflect từ
            // chính đòn đó) thì gauge giữ nguyên, không mất trắng, để hero khác dùng sau.
            if (skill.SlotIndex == 4 && _currentActor.Side == TeamSide.Player && _currentActor.IsAlive)
                State.ConsumeUltimate();

            FinishTurn(skill.Data.ExtraAtbCost);
        }

        /// <summary>task-consumable-items.md — dùng vật phẩm tốn trọn lượt (giống Guard/Escape),
        /// không qua Action Command minigame. <see cref="ActionIntent.SkillSlot"/> tái dùng làm
        /// chỉ số <see cref="ItemType"/>. Smoke Bomb xử lý RIÊNG ở đây (không qua
        /// <see cref="ItemResolver"/>) vì cần gọi <see cref="Finish"/> — <see cref="ItemResolver"/>
        /// không có quyền đổi <see cref="SimPhase"/>.</summary>
        private void ExecuteUseItem(in ActionIntent intent)
        {
            var type = (ItemType)intent.SkillSlot;
            if (!State.ItemLoadout.TryGetValue(type, out int remaining) || remaining <= 0)
            {
                FinishTurn(0);
                return;
            }

            if (type == ItemType.SmokeBomb)
            {
                ConsumeItem(type);
                Finish(BattleResult.Escaped);
                return;
            }

            if (_items.Use(type, _currentActor, State.Rng))
                ConsumeItem(type);

            FinishTurn(0);
        }

        private void ConsumeItem(ItemType type)
        {
            State.ItemLoadout[type]--;
            State.ItemsUsed.TryGetValue(type, out int used);
            State.ItemsUsed[type] = used + 1;
        }

        /// <summary>Bước 13–14.</summary>
        private void FinishTurn(int extraAtbCost)
        {
            Phase = SimPhase.AfterEffects;

            var actor = _currentActor;

            // Bước 13: giảm duration buff/debuff CỦA ACTOR, trừ ATB
            if (actor != null)
            {
                _status.TickTurnEnd(actor);
                _poise.TickTurnEnd(actor);
                // task-setbonus.md — bộ Guardian (hồi HP mỗi khi kết thúc lượt). Đặt sau 2 Tick ở
                // trên (đúng thứ tự "kết thúc lượt" nhất quán với Status/Poise), trước khi trừ ATB.
                _passive.TriggerOnTurnEnd(actor, State.Rng);
                _scheduler.ConsumeTurn(actor, extraAtbCost);
                Events.Emit(CombatEventType.TurnEnded, actor.Id);
            }

            // Bước 14: dọn dẹp
            ActionResolver.TickDownedUnits(State);
            CleanupOrphanMinions();
            AdvanceRowsIfFrontEmpty();
            RefreshIntents();

            CheckEnd();
            if (!IsFinished) Phase = NextTurnOrRoundEndPhase();
        }

        private void FinishTurnSkipped()
        {
            if (_currentActor != null)
            {
                _status.TickTurnEnd(_currentActor);
                _poise.TickTurnEnd(_currentActor);
                _scheduler.ConsumeTurn(_currentActor);
                Events.Emit(CombatEventType.TurnEnded, _currentActor.Id);
            }
            ActionResolver.TickDownedUnits(State);
            CheckEnd();
            if (!IsFinished) Phase = NextTurnOrRoundEndPhase();
        }

        /// <summary>task-setbonus.md §1.3 — phát hiện phụ khi làm Tempest: SimPhase.RoundEnd chưa
        /// bao giờ được gán ở đâu cả (dead code trong switch của Advance()), nên BeginRound() chỉ
        /// từng chạy 1 lần lúc Start() — State.RoundNumber không bao giờ tăng, và "mỗi round" của
        /// Tempest thực ra chỉ có ý nghĩa "1 lần duy nhất cả trận". Sửa: khi MỌI unit còn sống đều
        /// đã HasActedThisRound, round hiện tại thật sự đã xong — chuyển RoundEnd (Advance() tự
        /// nối sang RoundStart → BeginRound() thật sự chạy lại).</summary>
        private SimPhase NextTurnOrRoundEndPhase()
            => AllAliveUnitsActedThisRound() ? SimPhase.RoundEnd : SimPhase.TurnStart;

        private bool AllAliveUnitsActedThisRound()
        {
            for (int i = 0; i < State.Units.Count; i++)
            {
                var u = State.Units[i];
                if (u.IsAlive && !u.HasActedThisRound) return false;
            }
            return true;
        }

        // =====================================================================
        // HỖ TRỢ
        // =====================================================================

        private bool TryEscape()
        {
            float playerSpd = 0f, enemySpd = 0f;
            for (int i = 0; i < State.Units.Count; i++)
            {
                var u = State.Units[i];
                if (!u.IsAlive) continue;
                if (u.Side == TeamSide.Player) playerSpd += u.SpdEffective;
                else enemySpd += u.SpdEffective;
            }
            float chance = 0.40f + (playerSpd - enemySpd) * 0.005f;
            return State.Rng.Chance(Core.Maths.GameMath.Clamp(chance, 0.05f, 0.95f));
        }

        /// <summary>Edge case E20: minion mất chủ thì biến mất.</summary>
        private void CleanupOrphanMinions()
        {
            for (int i = 0; i < State.Units.Count; i++)
            {
                var u = State.Units[i];
                if (!u.IsMinion || u.IsDead) continue;
                var owner = u.OwnerId >= 0 ? State.GetUnit(u.OwnerId) : null;
                if (owner == null || owner.IsDead)
                {
                    u.Hp = 0;
                    Events.Emit(CombatEventType.UnitDied, target: u.Id);
                }
            }
        }

        /// <summary>Edge case E14: hàng trước chết hết → hàng sau lên, ở ĐẦU lượt kế.</summary>
        private void AdvanceRowsIfFrontEmpty()
        {
            AdvanceSide(TeamSide.Player);
            AdvanceSide(TeamSide.Enemy);

            void AdvanceSide(TeamSide side)
            {
                bool anyFront = false;
                for (int i = 0; i < State.Units.Count; i++)
                {
                    var u = State.Units[i];
                    if (u.Side == side && u.IsAlive && u.Row == Row.Front) { anyFront = true; break; }
                }
                if (anyFront) return;

                for (int i = 0; i < State.Units.Count; i++)
                {
                    var u = State.Units[i];
                    if (u.Side == side && u.IsAlive && u.Row == Row.Back) u.Row = Row.Front;
                }
            }
        }

        /// <summary>Tính lại intent của mọi enemy để hiện preview (peek, không tiêu seed).</summary>
        private void RefreshIntents()
        {
            for (int i = 0; i < State.Units.Count; i++)
            {
                var u = State.Units[i];
                if (u.Side != TeamSide.Enemy || !u.IsAlive) continue;
                _aiProfiles.TryGetValue(u.Id, out var profile);
                var d = _ai.Choose(u, profile, 0, State.Rng, peek: true);
                if (d.IsValid)
                    Events.Emit(CombatEventType.IntentChanged, u.Id, d.TargetId,
                                intValue: (int)d.Skill.Data.Type, stringValue: d.Skill.Data.Id);
            }
        }

        private void CheckEnd()
        {
            var r = State.EvaluateResult();
            // Edge case E12 (plan.md §4.14): trước khi chốt Defeat, cho AutoRevive (nếu có) 1 cơ
            // hội cứu trận — TryAutoRevive tự kiểm tra unit nào mang passive OnTeamWipe, không
            // tốn seed nếu không unit nào đủ điều kiện.
            if (r == BattleResult.Defeat && _passive.TryAutoRevive(State.Rng))
                r = State.EvaluateResult();

            // Tháp Vô Tận: Victory (Enemy wipe) không tự kết thúc trận nếu còn tầng tiếp theo —
            // cho OnEnemyWaveCleared cơ hội spawn đợt mới trước khi Finish thật.
            if (r == BattleResult.Victory && OnEnemyWaveCleared != null && OnEnemyWaveCleared(this))
            {
                RefreshIntents(); // đợt địch mới → tính lại intent hiển thị ngay
                return;
            }

            if (r != BattleResult.InProgress) Finish(r);
        }

        private void Finish(BattleResult result)
        {
            State.Result = result;
            Phase = SimPhase.Finished;
            Events.Emit(CombatEventType.BattleEnded, intValue: State.TurnCounter,
                        intValue2: State.PerfectCount, result: result);
        }

        /// <summary>plan.md §4.15 — màn Defeat, lựa chọn "Hồi sinh bằng Gem". Chỉ gọi được khi trận
        /// đã chốt Defeat thật (AutoRevive nội bộ ở <see cref="CheckEnd"/> đã thử và không cứu được).
        /// Hồi TOÀN BỘ hero về 40% MaxHP — ĐÚNG <c>RevivePercent</c> đã dùng cho Revive Feather
        /// (task-consumable-items.md, <see cref="Systems.ItemResolver"/>), không bịa tỉ lệ mới. Trừ
        /// Gem là việc của tầng Meta (Game.Combat không được tham chiếu Game.Services, xem
        /// AssemblyRuleTests) — gọi hàm này SAU KHI đã trừ Gem thành công ở lớp gọi.
        /// Đặt lại <see cref="BattleState.Result"/> về InProgress — vòng lặp <c>Update()</c> ở
        /// BattleSceneInstaller tự nhận biết qua <see cref="IsFinished"/> mỗi frame (không cần gọi
        /// resume riêng); <c>Phase</c> vẫn là Finished nhưng nhánh `default` của <c>Advance()</c> tự
        /// đưa về TurnStart an toàn.</summary>
        public bool TryReviveWithGem()
        {
            if (State.Result != BattleResult.Defeat) return false;

            bool anyRevived = false;
            for (int i = 0; i < State.Units.Count; i++)
            {
                var u = State.Units[i];
                if (u.Side != TeamSide.Player || u.IsAlive) continue;
                u.SetHp(Core.Maths.GameMath.Max(1, Core.Maths.GameMath.FloorToInt(u.MaxHp * 0.4f)));
                Events.Emit(CombatEventType.UnitRevived, u.Id, u.Id, intValue: u.Hp);
                anyRevived = true;
            }
            if (!anyRevived) return false;

            State.Result = BattleResult.InProgress;
            return true;
        }

        /// <summary>Chạy trọn trận không cần input (dùng cho test, balance harness, auto-battle).</summary>
        public BattleResult RunToCompletion(System.Func<CombatSimulation, ActionIntent> playerPolicy = null)
        {
            if (Phase == SimPhase.Init) Start();

            int guard = 0;
            while (!IsFinished)
            {
                if (++guard > 10_000) { Finish(BattleResult.Timeout); break; }

                if (Advance())
                {
                    var intent = playerPolicy?.Invoke(this) ?? DefaultAutoIntent();
                    SubmitIntent(intent);
                }
            }
            return State.Result;
        }

        /// <summary>Policy Auto 7 ưu tiên cho player — plan.md §4.16.</summary>
        public ActionIntent DefaultAutoIntent()
        {
            var actor = _currentActor;
            bool ultReady = State.IsUltimateReady;
            int enemy = AutoFirstAliveEnemyId();

            // 1. Đồng minh HP < 35% → heal
            if (HasLowHpAlly(actor))
            {
                var h = FindHealSkill(actor, ultReady);
                if (h != null) return new ActionIntent(actor.Id, h.SlotIndex, enemy, CommandGrade.Good);
            }

            // 2. Đồng minh gục → revive
            if (HasDeadAlly(actor))
            {
                var r = FindReviveSkill(actor, ultReady);
                if (r != null) return new ActionIntent(actor.Id, r.SlotIndex, -1, CommandGrade.Good);
            }

            // 3. Địch đang Break → damage cao nhất
            if (HasBrokenEnemy(out int brokenId))
            {
                var d = FindHighestDamageSkill(actor, ultReady, excludeUlt: true);
                if (d != null) return new ActionIntent(actor.Id, d.SlotIndex, brokenId, CommandGrade.Good);
            }

            // 4. Ultimate đầy + ≥2 địch sống
            if (ultReady && State.CountAlive(TeamSide.Enemy) >= 2)
            {
                var ult = FindUltimateSkill(actor);
                if (ult != null && actor.CanUseSkill(ult, ultReady))
                    return new ActionIntent(actor.Id, 4, enemy, CommandGrade.Good);
            }

            // 5. Skill khắc chế element mục tiêu
            if (enemy >= 0)
            {
                var ec = FindElementCounterSkill(actor, enemy, ultReady);
                if (ec != null) return new ActionIntent(actor.Id, ec.SlotIndex, enemy, CommandGrade.Good);
            }

            // 6. Damage skill cao nhất còn dùng được (trừ ultimate slot)
            var best = FindHighestDamageSkill(actor, ultReady, excludeUlt: true);
            if (best != null) return new ActionIntent(actor.Id, best.SlotIndex, enemy, CommandGrade.Good);

            // 7. Đánh thường (slot 0)
            return new ActionIntent(actor.Id, 0, enemy, CommandGrade.Good);
        }

        private SkillRuntime FindUltimateSkill(CombatUnit actor)
        {
            foreach (var sk in actor.Skills)
                if (sk.SlotIndex == 4) return sk;
            return null;
        }

        private int AutoFirstAliveEnemyId()
        {
            var units = State.Units;
            for (int i = 0; i < units.Count; i++)
                if (units[i].Side == TeamSide.Enemy && units[i].IsAlive) return units[i].Id;
            return -1;
        }

        private bool HasLowHpAlly(CombatUnit actor)
        {
            var units = State.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Side == actor.Side && u.IsAlive && u.MaxHp > 0 && u.Hp < u.MaxHp * 0.35f)
                    return true;
            }
            return false;
        }

        private bool HasDeadAlly(CombatUnit actor)
        {
            var units = State.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Side == actor.Side && !u.IsAlive) return true;
            }
            return false;
        }

        private bool HasBrokenEnemy(out int targetId)
        {
            var units = State.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Side == TeamSide.Enemy && u.IsAlive && u.IsBroken)
                {
                    targetId = u.Id;
                    return true;
                }
            }
            targetId = -1;
            return false;
        }

        private SkillRuntime FindHealSkill(CombatUnit actor, bool ultReady)
        {
            SkillRuntime best = null;
            foreach (var sk in actor.Skills)
            {
                if (sk.SlotIndex == 4) continue;
                if (!actor.CanUseSkill(sk, ultReady)) continue;
                var d = sk.Data;
                if ((d.Type == SkillType.Heal || d.HealPower > 0f) && d.TargetsAllies)
                    best = sk;
            }
            return best;
        }

        private SkillRuntime FindReviveSkill(CombatUnit actor, bool ultReady)
        {
            foreach (var sk in actor.Skills)
            {
                if (!actor.CanUseSkill(sk, ultReady)) continue;
                if (sk.Data.RevivePercent > 0f) return sk;
            }
            return null;
        }

        private SkillRuntime FindHighestDamageSkill(CombatUnit actor, bool ultReady, bool excludeUlt)
        {
            SkillRuntime best = null;
            float bestPower = 0f;
            foreach (var sk in actor.Skills)
            {
                if (excludeUlt && sk.SlotIndex == 4) continue;
                if (!actor.CanUseSkill(sk, ultReady)) continue;
                if (!sk.Data.DealsDamage) continue;
                float power = sk.Data.PowerMultiplier * sk.Data.HitCount;
                if (power > bestPower) { bestPower = power; best = sk; }
            }
            return best;
        }

        private SkillRuntime FindElementCounterSkill(CombatUnit actor, int targetId, bool ultReady)
        {
            CombatUnit target = null;
            var units = State.Units;
            for (int i = 0; i < units.Count; i++)
                if (units[i].Id == targetId) { target = units[i]; break; }
            if (target == null) return null;

            foreach (var sk in actor.Skills)
            {
                if (sk.SlotIndex == 4) continue;
                if (!actor.CanUseSkill(sk, ultReady)) continue;
                if (!sk.Data.DealsDamage) continue;
                if (ElementTable.IsStrong(sk.Data.Element, target.Element)) return sk;
            }
            return null;
        }
    }
}
