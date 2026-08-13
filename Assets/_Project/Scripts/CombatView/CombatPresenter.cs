using System.Collections;
using System.Collections.Generic;
using Game.Combat;
using Game.Combat.Events;
using Game.CombatView.Effects;
using Game.CombatView.Units;
using Game.Core;
using Game.Data;
using Game.Services.Audio;
using Game.Services.Settings;
using UnityEngine;

namespace Game.CombatView
{
    /// <summary>
    /// Đọc CombatEventQueue và diễn hoạt TUẦN TỰ — plan.md §11.3.
    /// Simulation chạy xong tức thì; Presenter mới là thứ tốn thời gian thực.
    /// Tách như vậy nên đổi tốc độ ×1/×2/×3 không ảnh hưởng kết quả trận.
    /// </summary>
    public sealed class CombatPresenter : MonoBehaviour
    {
        [Header("Tốc độ")]
        [Range(1, 3)][SerializeField] private int _speed = 1;

        [Header("Nhịp diễn hoạt (giây, ở tốc độ ×1)")]
        [SerializeField] private float _damageBeat = 0.28f;
        [SerializeField] private float _statusBeat = 0.16f;
        [SerializeField] private float _turnBeat = 0.10f;
        [SerializeField] private float _deathBeat = 0.45f;
        [SerializeField] private float _breakBeat = 0.55f;

        private readonly Dictionary<int, UnitView> _views = new();
        private FloatingTextLayer _floating;
        private VfxPlayer _vfx;
        private IAudioService _audio;
        private Camera _camera;
        private CameraFx _cameraFx;

        private CombatEventQueue _queue;
        private Coroutine _playback;

        /// <summary>Đổi BGM khi vào trận sang bgm_boss thay vì bgm_battle — Meta gán qua BattleSceneInstaller.</summary>
        public bool IsBossFight { get; set; }

        public bool IsPlaying { get; private set; }
        public int Speed { get => _speed; set => _speed = Mathf.Clamp(value, 1, 3); }

        private float Beat(float seconds) => seconds / _speed;

        // =====================================================================

        public void Setup(CombatEventQueue queue, FloatingTextLayer floating, VfxPlayer vfx, Camera cam)
        {
            _queue = queue;
            _floating = floating;
            _vfx = vfx;
            _camera = cam != null ? cam : Camera.main;
            ServiceLocator.TryGet(out _audio);

            if (_camera != null)
            {
                _cameraFx = _camera.GetComponent<CameraFx>();
                if (_cameraFx == null) _cameraFx = _camera.gameObject.AddComponent<CameraFx>();

                if (ServiceLocator.TryGet<ISettingsService>(out var settings))
                {
                    _cameraFx.ShakeEnabled = settings.Current.ScreenShake;
                    settings.OnChanged += s => _cameraFx.ShakeEnabled = s.ScreenShake;
                }
            }
        }

        public void RegisterView(UnitView view) => _views[view.UnitId] = view;

        public void ClearViews() => _views.Clear();

        /// <summary>Diễn hết event đang chờ trong hàng đợi.</summary>
        public void PlayPending()
        {
            if (_playback != null) return;
            _playback = StartCoroutine(PlaybackRoutine());
        }

        private IEnumerator PlaybackRoutine()
        {
            IsPlaying = true;

            while (_queue.TryDequeue(out var evt))
            {
                float wait = Present(evt);
                if (wait > 0f) yield return new WaitForSeconds(wait);
            }

            IsPlaying = false;
            _playback = null;
        }

        // =====================================================================
        // Bảng phân phối event — đối chiếu object-map.md §5.1
        // =====================================================================

        private float Present(in CombatEvent e)
        {
            switch (e.Type)
            {
                case CombatEventType.BattleStarted:
                    _audio?.PlayMusic(IsBossFight ? "bgm_boss" : "bgm_battle");
                    return Beat(0.3f);

                case CombatEventType.ActionDeclared:
                {
                    if (TryView(e.SourceUnitId, out var actor)) actor.PlayAttackLunge();
                    return Beat(0.16f);
                }

                case CombatEventType.DamageDealt:
                    return PresentDamage(e);

                case CombatEventType.HealApplied:
                {
                    if (e.IntValue <= 0) return 0f;
                    if (TryView(e.TargetUnitId, out var v))
                    {
                        _floating.ShowHeal(Above(v), e.IntValue);
                        _vfx?.Play("heal", v.transform.position);
                    }
                    _audio?.PlaySfx("battle/sfx_battle_heal");
                    return Beat(_statusBeat);
                }

                case CombatEventType.StatusApplied:
                {
                    if (TryView(e.TargetUnitId, out var v))
                    {
                        _floating.ShowText(Above(v), StatusLabel(e.Status),
                                           StatusColor(e.Status), 0.85f);
                        // Buff/debuff trước đây chỉ có text + SFX, không có hiệu ứng hình —
                        // tái dùng bộ VFX sẵn có (heal=sparkle sáng cho buff, dark=quầng tối cho debuff)
                        // để mỗi lần dính status đều có phản hồi thị giác, không chỉ chữ bay.
                        _vfx?.Play(IsBuff(e.Status) ? "heal" : "dark", v.transform.position, 0.7f);
                    }
                    _audio?.PlaySfx(IsBuff(e.Status)
                        ? "battle/sfx_battle_buff" : "battle/sfx_battle_debuff");
                    return Beat(_statusBeat);
                }

                case CombatEventType.StatusResisted:
                {
                    if (TryView(e.TargetUnitId, out var v)) _floating.ShowResist(Above(v));
                    _audio?.PlaySfx("battle/sfx_battle_resist");
                    return Beat(_statusBeat);
                }

                case CombatEventType.StatusTicked:
                {
                    if (TryView(e.TargetUnitId, out var v))
                    {
                        v.PlayHit();
                        _floating.ShowDamage(Above(v), e.IntValue, false);
                        _vfx?.Play("poison", v.transform.position, 0.6f);
                    }
                    return Beat(_statusBeat);
                }

                case CombatEventType.PoiseBroken:
                {
                    if (TryView(e.TargetUnitId, out var v))
                    {
                        _floating.ShowBreak(Above(v, 0.9f));
                        v.PlayHit();
                        _vfx?.Play("break", v.transform.position, 1.3f);
                    }
                    _audio?.PlaySfx("battle/sfx_battle_break");
                    _cameraFx?.Flash(Color.white, 0.25f);
                    _cameraFx?.Shake(0.18f, Beat(0.3f));
                    StartCoroutine(HitStop(0.12f));
                    return Beat(_breakBeat);
                }

                case CombatEventType.ShieldAbsorbed:
                {
                    if (TryView(e.TargetUnitId, out var v))
                        _vfx?.Play("shield", v.transform.position, 0.9f);
                    return 0f;
                }

                case CombatEventType.UnitDied:
                {
                    if (TryView(e.TargetUnitId, out var v)) v.PlayDeath();
                    _audio?.PlaySfx("battle/sfx_battle_death");
                    return Beat(_deathBeat);
                }

                case CombatEventType.UnitRevived:
                {
                    if (TryView(e.TargetUnitId, out var v))
                    {
                        v.PlayRevive();
                        _floating.ShowHeal(Above(v), e.IntValue);
                    }
                    return Beat(_deathBeat);
                }

                case CombatEventType.TurnStarted:
                    return Beat(_turnBeat);

                case CombatEventType.BattleEnded:
                    _audio?.PlayMusic(e.Result == BattleResult.Victory
                        ? "bgm_victory" : "bgm_defeat", loop: false);
                    return Beat(0.5f);

                // Các event chỉ để UI cập nhật số, không tốn thời gian diễn
                case CombatEventType.SpChanged:
                case CombatEventType.CooldownChanged:
                case CombatEventType.IntentChanged:
                case CombatEventType.UltimateCharged:
                case CombatEventType.PoiseDamaged:
                case CombatEventType.StatusExpired:
                default:
                    return 0f;
            }
        }

        private float PresentDamage(in CombatEvent e)
        {
            if (!TryView(e.TargetUnitId, out var target)) return 0f;

            // IntValue < 0 là quy ước MISS (DamageResult.Miss)
            if (e.IntValue < 0)
            {
                _floating.ShowMiss(Above(target));
                target.PlayMiss();
                _audio?.PlaySfx("battle/sfx_battle_miss");
                return Beat(_damageBeat * 0.6f);
            }

            target.PlayHit();
            _floating.ShowDamage(Above(target), e.IntValue, e.Flag);

            // VFX key: đặc biệt cho inferno_bulwark (Bloom riêng), magic bolt cho Neutral magical,
            // còn lại dùng KeyForElement — Wind→"wind", Light→"light" đã đúng sau Phase 5 fix.
            string vfxKey = ResolveVfxKey(e);
            _vfx?.Play(vfxKey, target.transform.position);

            _audio?.PlaySfx(ElementSfx(e.Element));
            _cameraFx?.Shake(0.05f, Beat(0.08f));

            if (e.Flag)
            {
                _audio?.PlaySfx("battle/sfx_battle_crit");
                _cameraFx?.ZoomPulse(1.05f, Beat(0.15f));
                _cameraFx?.Shake(0.09f, Beat(0.15f));
            }

            if (e.Grade == CommandGrade.Perfect)
            {
                _floating.ShowPerfect(Above(target, 1.2f));
                _cameraFx?.Flash(new Color(1f, 0.82f, 0.4f), 0.2f);
                _cameraFx?.Shake(0.1f, Beat(0.2f));
            }

            StartCoroutine(HitStop(e.Flag ? 0.09f : 0.05f));
            return Beat(_damageBeat);
        }

        // =====================================================================

        private IEnumerator HitStop(float seconds)
        {
            // Dùng unscaled time để hit-stop không tự đóng băng chính nó
            float prev = Time.timeScale;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds / _speed);
            Time.timeScale = prev;
        }

        private bool TryView(int unitId, out UnitView view) => _views.TryGetValue(unitId, out view);

        // Neutral magical skills chơi "magic" VFX thay vì "slash" (physical default).
        // Không check ở runtime data vì CombatEvent không carry DamageType — dùng whitelist nhỏ.
        private static readonly HashSet<string> _neutralMagicSkills = new()
        {
            "skill_elemental_strike", "skill_ultimate_nova",
        };

        private static string ResolveVfxKey(Combat.Events.CombatEvent e)
        {
            if (e.StringValue == "skill_inferno_bulwark" && e.Status == StatusId.None)
                return "inferno_bulwark";
            if (e.Element == Element.Neutral && _neutralMagicSkills.Contains(e.StringValue))
                return "magic";
            return VfxPlayer.KeyForElement(e.Element);
        }

        private static Vector3 Above(UnitView v, float extra = 0f)
            => v.transform.position + new Vector3(0f, 1.2f + extra, 0f);

        private static string ElementSfx(Element e) => e switch
        {
            Element.Fire  => "battle/sfx_battle_hit_fire",
            Element.Water => "battle/sfx_battle_hit_water",
            Element.Earth => "battle/sfx_battle_hit_earth",
            Element.Wind  => "battle/sfx_battle_hit_wind",
            Element.Light => "battle/sfx_battle_hit_light",
            Element.Dark  => "battle/sfx_battle_hit_dark",
            _ => "battle/sfx_battle_hit_physical"
        };

        private static bool IsBuff(StatusId id)
            => Game.Combat.Model.StatusTable.IsBuff(id);

        private static Color StatusColor(StatusId id)
            => IsBuff(id) ? FloatingTextLayer.COLOR_HEAL : FloatingTextLayer.COLOR_CRIT;

        private static string StatusLabel(StatusId id) => id switch
        {
            StatusId.Burn => "BURN", StatusId.Poison => "POISON", StatusId.Bleed => "BLEED",
            StatusId.Stun => "STUN", StatusId.Freeze => "FREEZE", StatusId.Silence => "SILENCE",
            StatusId.Taunt => "TAUNT", StatusId.Sleep => "SLEEP", StatusId.Paralyze => "PARALYZE",
            StatusId.AtkDown => "ATK-", StatusId.DefDown => "DEF-", StatusId.SpdDown => "SLOW",
            StatusId.Blind => "BLIND", StatusId.Curse => "CURSE",
            StatusId.AtkUp => "ATK+", StatusId.DefUp => "DEF+", StatusId.SpdUp => "HASTE",
            StatusId.Shield => "SHIELD", StatusId.Regen => "REGEN", StatusId.Immunity => "IMMUNE",
            StatusId.Counter => "COUNTER", StatusId.Reflect => "REFLECT",
            _ => id.ToString().ToUpperInvariant()
        };
    }
}
