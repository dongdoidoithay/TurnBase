using System.Collections.Generic;
using Game.Combat.Model;
using Game.Core.Random;
using Game.Data;

namespace Game.Combat.Systems
{
    /// <summary>
    /// 11 chế độ nhắm mục tiêu — plan.md §4.12.
    /// Thứ tự ưu tiên ghi đè: Taunt > người chơi chọn > auto-suggest.
    /// </summary>
    public sealed class TargetSelector
    {
        private readonly BattleState _state;
        private readonly List<CombatUnit> _buffer = new(8);
        private readonly List<CombatUnit> _pool = new(8);

        public TargetSelector(BattleState state) => _state = state;

        /// <summary>
        /// Giải mục tiêu cuối cùng. <paramref name="preferredId"/> là lựa chọn của người chơi (-1 = tự chọn).
        /// Kết quả DÙNG NGAY, không giữ lại (buffer tái sử dụng).
        /// </summary>
        public List<CombatUnit> Resolve(CombatUnit actor, SkillData skill, int preferredId, IRandomSource rng)
        {
            _buffer.Clear();
            var enemySide = _state.Opposite(actor.Side);

            switch (skill.Target)
            {
                case TargetMode.Self:
                    _buffer.Add(actor);
                    break;

                case TargetMode.AllEnemies:
                    CollectAlive(enemySide, _buffer);
                    break;

                case TargetMode.AllAllies:
                    CollectAlive(actor.Side, _buffer);
                    break;

                case TargetMode.FrontRowEnemies:
                    CollectRow(enemySide, Row.Front, _buffer);
                    if (_buffer.Count == 0) CollectAlive(enemySide, _buffer);
                    break;

                case TargetMode.BackRowEnemies:
                    CollectRow(enemySide, Row.Back, _buffer);
                    if (_buffer.Count == 0) CollectAlive(enemySide, _buffer);
                    break;

                case TargetMode.RandomEnemy:
                    CollectAlive(enemySide, _pool);
                    for (int i = 0; i < skill.RandomTargetCount && _pool.Count > 0; i++)
                        _buffer.Add(_pool[rng.NextInt(_pool.Count)]);
                    break;

                case TargetMode.LowestHpEnemy:
                    AddIfNotNull(LowestHpPercent(enemySide));
                    break;

                case TargetMode.LowestHpAlly:
                    AddIfNotNull(LowestHpPercent(actor.Side));
                    break;

                case TargetMode.DeadAlly:
                    AddIfNotNull(FirstRevivable(actor.Side));
                    break;

                case TargetMode.SingleAlly:
                    AddIfNotNull(ResolveSingle(actor, actor.Side, preferredId, allowDead: false));
                    break;

                case TargetMode.SingleEnemy:
                default:
                    AddIfNotNull(ResolveSingleEnemy(actor, enemySide, preferredId));
                    break;
            }

            return _buffer;

            void AddIfNotNull(CombatUnit u) { if (u != null) _buffer.Add(u); }
        }

        /// <summary>Đơn mục tiêu địch — Taunt ghi đè mọi lựa chọn.</summary>
        private CombatUnit ResolveSingleEnemy(CombatUnit actor, TeamSide side, int preferredId)
        {
            var taunter = FindTaunter(side);
            if (taunter != null) return taunter;
            return ResolveSingle(actor, side, preferredId, allowDead: false);
        }

        private CombatUnit ResolveSingle(CombatUnit actor, TeamSide side, int preferredId, bool allowDead)
        {
            if (preferredId >= 0)
            {
                var u = _state.GetUnit(preferredId);
                if (u != null && u.Side == side && (allowDead || u.IsAlive)) return u;
            }
            return AutoSuggest(actor, side);
        }

        /// <summary>Gợi ý mục tiêu: ưu tiên địch bị khắc chế, sau đó HP% thấp nhất.</summary>
        public CombatUnit AutoSuggest(CombatUnit actor, TeamSide side)
        {
            var taunter = FindTaunter(side);
            if (taunter != null) return taunter;

            CollectAlive(side, _pool);
            if (_pool.Count == 0) return null;

            CombatUnit best = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < _pool.Count; i++)
            {
                var u = _pool[i];
                float score = 0f;

                if (ElementTable.IsStrong(actor.Element, u.Element)) score += 50f;
                if (ElementTable.IsWeak(actor.Element, u.Element)) score -= 30f;
                if (u.IsBroken) score += 40f;

                // HP% thấp → ưu tiên dứt điểm
                score += (1f - (float)u.Hp / u.MaxHp) * 30f;
                // Hàng trước dễ đánh hơn với đòn vật lý
                if (u.Row == Row.Front) score += 5f;

                if (score > bestScore) { bestScore = score; best = u; }
            }

            return best;
        }

        public CombatUnit FindTaunter(TeamSide side)
        {
            for (int i = 0; i < _state.Units.Count; i++)
            {
                var u = _state.Units[i];
                // Edge case E07: unit taunt đã chết thì không còn ghi đè
                if (u.Side == side && u.IsAlive && u.HasStatus(StatusId.Taunt)) return u;
            }
            return null;
        }

        /// <summary>Chọn lại mục tiêu khi target chết giữa combo (edge case E01).</summary>
        public CombatUnit ReplacementTarget(CombatUnit actor, CombatUnit deadTarget)
        {
            return AutoSuggest(actor, deadTarget.Side);
        }

        // ===== Helper =====

        private void CollectAlive(TeamSide side, List<CombatUnit> into)
        {
            into.Clear();
            for (int i = 0; i < _state.Units.Count; i++)
            {
                var u = _state.Units[i];
                if (u.Side == side && u.IsAlive) into.Add(u);
            }
        }

        private void CollectRow(TeamSide side, Row row, List<CombatUnit> into)
        {
            into.Clear();
            for (int i = 0; i < _state.Units.Count; i++)
            {
                var u = _state.Units[i];
                if (u.Side == side && u.IsAlive && u.Row == row) into.Add(u);
            }
        }

        private CombatUnit LowestHpPercent(TeamSide side)
        {
            CombatUnit best = null;
            float bestPct = float.MaxValue;
            for (int i = 0; i < _state.Units.Count; i++)
            {
                var u = _state.Units[i];
                if (u.Side != side || !u.IsAlive) continue;
                float pct = (float)u.Hp / u.MaxHp;
                if (pct < bestPct) { bestPct = pct; best = u; }
            }
            return best;
        }

        /// <summary>Đồng minh gục sớm nhất còn hồi sinh được (edge case E11).</summary>
        private CombatUnit FirstRevivable(TeamSide side)
        {
            CombatUnit best = null;
            int mostTurnsDown = -1;
            for (int i = 0; i < _state.Units.Count; i++)
            {
                var u = _state.Units[i];
                if (u.Side != side || u.IsAlive || u.PermanentlyDown) continue;
                if (u.TurnsDown > mostTurnsDown) { mostTurnsDown = u.TurnsDown; best = u; }
            }
            return best;
        }
    }
}
