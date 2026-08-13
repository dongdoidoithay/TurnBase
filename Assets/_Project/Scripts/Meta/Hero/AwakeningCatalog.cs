using Game.Combat.Model;
using Game.Data;

namespace Game.Meta.Hero
{
    /// <summary>
    /// Bộ Awakening passive thật cho 6 hero hiện có — task-ascend.md §7 mục A.4. Kích hoạt khi
    /// hero đạt ★6 (<see cref="AscendSystem.MAX_STAR"/>, tương đương <c>HeroInstanceDto.Awakened</c>).
    /// Hard-code (KHÔNG qua ScriptableObject): <see cref="StatModifier"/>/<see cref="StatusApplication"/>
    /// chứa field readonly mà Unity Inspector không serialize được (bị bỏ qua, luôn hiện rỗng) —
    /// cùng lý do <see cref="AscendSystem"/> hard-code bảng chi phí thay vì dùng SO. Nếu sau này
    /// cần content editor thật cho passive, phải làm 1 DTO [Serializable] riêng để mirror rồi convert.
    ///
    /// V1 — số liệu (stack/lượt/%) là PLACEHOLDER chờ playtest/Balance Harness, không phải cân bằng
    /// cuối. Mỗi <see cref="Get"/> trả về instance MỚI (không share) để <c>Consumed</c> không rò rỉ
    /// giữa các trận/unit khác nhau.
    /// </summary>
    public static class AwakeningCatalog
    {
        public static PassiveData Get(string heroDefId) => heroDefId switch
        {
            // Vanguard/Fire — tank hoá vĩnh viễn ngay đầu trận.
            "hero_ember_knight" => new PassiveData
            {
                Id = "awaken_ember_knight_molten_bulwark",
                Trigger = PassiveTrigger.OnBattleStart,
                Modifiers = new[]
                {
                    new StatModifier(StatType.DefPct, 15f, "awakening"),
                    new StatModifier(StatType.MaxHpPct, 10f, "awakening"),
                }
            },

            // Slayer/Dark — ăn kill snowball ATK.
            "hero_shadow_fang" => new PassiveData
            {
                Id = "awaken_shadow_fang_night_executioner",
                Trigger = PassiveTrigger.OnKill,
                Applies = new[]
                {
                    new StatusApplication(StatusId.AtkUp, 1f, duration: 2, stacks: 2, targetSelf: true),
                }
            },

            // Arcanist/Water — kiểm soát tốc độ mỗi cú đánh trúng.
            "hero_frost_sage" => new PassiveData
            {
                Id = "awaken_frost_sage_absolute_zero",
                Trigger = PassiveTrigger.OnHitDealt,
                Applies = new[]
                {
                    new StatusApplication(StatusId.SpdDown, 0.5f, duration: 2, stacks: 1, targetSelf: false),
                }
            },

            // Warden/Light — bảo hộ bản thân khi hồi máu cho đồng đội.
            "hero_dawn_cleric" => new PassiveData
            {
                Id = "awaken_dawn_cleric_radiant_ward",
                Trigger = PassiveTrigger.OnHealDone,
                Applies = new[]
                {
                    new StatusApplication(StatusId.DefUp, 1f, duration: 2, stacks: 1, targetSelf: true),
                }
            },

            // Trickster/Wind — thưởng tốc độ khi bấm lệnh Perfect.
            "hero_gale_thief" => new PassiveData
            {
                Id = "awaken_gale_thief_windwalkers_gambit",
                Trigger = PassiveTrigger.OnPerfectCommand,
                Applies = new[]
                {
                    new StatusApplication(StatusId.SpdUp, 1f, duration: 2, stacks: 2, targetSelf: true),
                }
            },

            // Summoner/Dark — mạnh lên khi đồng đội gục (necromancer flavor).
            "hero_bone_caller" => new PassiveData
            {
                Id = "awaken_bone_caller_grave_pact",
                Trigger = PassiveTrigger.OnAllyDeath,
                Applies = new[]
                {
                    new StatusApplication(StatusId.AtkUp, 1f, duration: 3, stacks: 2, targetSelf: true),
                }
            },

            // ============================================================
            // 18 hero mở rộng (task-hero-roster.md) — GIỮ ĐÚNG trigger + hình dạng hiệu ứng của
            // hero mẫu cùng class (class mới là cái quyết định "cơ chế riêng" theo plan.md §5.1,
            // không phải element) — chỉ chỉnh SỐ LIỆU theo rarity (Legendary nhỉnh hơn) và, khi
            // hợp lý, đổi StatusId theo element (VD Arcanist: Water→SpdDown, Fire→Burn, Earth→
            // DefDown, Dark→Curse — giữ nguyên cách phối hệ đã dùng cho 18 skill Ultimate).
            // ============================================================

            // --- Vanguard (OnBattleStart, Def/HP vĩnh viễn đầu trận) ---
            "hero_iron_bastion" => new PassiveData
            {
                Id = "awaken_iron_bastion_unshakeable_wall",
                Trigger = PassiveTrigger.OnBattleStart,
                Modifiers = new[]
                {
                    new StatModifier(StatType.DefPct, 18f, "awakening"),
                    new StatModifier(StatType.MaxHpPct, 8f, "awakening"),
                }
            },
            "hero_tide_warden" => new PassiveData
            {
                Id = "awaken_tide_warden_tidal_resilience",
                Trigger = PassiveTrigger.OnBattleStart,
                Modifiers = new[]
                {
                    new StatModifier(StatType.DefPct, 10f, "awakening"),
                    new StatModifier(StatType.MaxHpPct, 16f, "awakening"),
                }
            },
            "hero_stormguard" => new PassiveData // Legendary — 3 modifier thay vì 2
            {
                Id = "awaken_stormguard_tempest_sovereign",
                Trigger = PassiveTrigger.OnBattleStart,
                Modifiers = new[]
                {
                    new StatModifier(StatType.DefPct, 15f, "awakening"),
                    new StatModifier(StatType.MaxHpPct, 12f, "awakening"),
                    new StatModifier(StatType.SpdPct, 8f, "awakening"),
                }
            },

            // --- Slayer (OnKill, AtkUp tự thân — snowball) ---
            "hero_blade_dancer" => new PassiveData
            {
                Id = "awaken_blade_dancer_lethal_grace",
                Trigger = PassiveTrigger.OnKill,
                Applies = new[] { new StatusApplication(StatusId.AtkUp, 1f, duration: 2, stacks: 2, targetSelf: true) }
            },
            "hero_crimson_reaver" => new PassiveData
            {
                Id = "awaken_crimson_reaver_bloodlust",
                Trigger = PassiveTrigger.OnKill,
                Applies = new[] { new StatusApplication(StatusId.AtkUp, 1f, duration: 2, stacks: 2, targetSelf: true) }
            },
            "hero_stone_breaker" => new PassiveData // Common — 1 stack thay vì 2
            {
                Id = "awaken_stone_breaker_rock_fury",
                Trigger = PassiveTrigger.OnKill,
                Applies = new[] { new StatusApplication(StatusId.AtkUp, 1f, duration: 2, stacks: 1, targetSelf: true) }
            },

            // --- Arcanist (OnHitDealt, debuff lên địch mỗi cú trúng — đổi StatusId theo hệ) ---
            "hero_pyromancer" => new PassiveData
            {
                Id = "awaken_pyromancer_scorching_mark",
                Trigger = PassiveTrigger.OnHitDealt,
                Applies = new[] { new StatusApplication(StatusId.Burn, 0.5f, duration: 2, stacks: 1, targetSelf: false) }
            },
            "hero_terra_seer" => new PassiveData
            {
                Id = "awaken_terra_seer_seismic_mark",
                Trigger = PassiveTrigger.OnHitDealt,
                Applies = new[] { new StatusApplication(StatusId.DefDown, 0.5f, duration: 2, stacks: 1, targetSelf: false) }
            },
            "hero_void_scholar" => new PassiveData // Legendary — chance cao hơn (0.6 thay vì 0.5)
            {
                Id = "awaken_void_scholar_abyssal_mark",
                Trigger = PassiveTrigger.OnHitDealt,
                Applies = new[] { new StatusApplication(StatusId.Curse, 0.6f, duration: 2, stacks: 1, targetSelf: false) }
            },

            // --- Warden (OnHealDone, DefUp tự thân khi hồi máu) ---
            "hero_grove_keeper" => new PassiveData
            {
                Id = "awaken_grove_keeper_natures_blessing",
                Trigger = PassiveTrigger.OnHealDone,
                Applies = new[] { new StatusApplication(StatusId.DefUp, 1f, duration: 2, stacks: 1, targetSelf: true) }
            },
            "hero_moon_priestess" => new PassiveData // Legendary — 2 stack thay vì 1
            {
                Id = "awaken_moon_priestess_lunar_blessing",
                Trigger = PassiveTrigger.OnHealDone,
                Applies = new[] { new StatusApplication(StatusId.DefUp, 1f, duration: 2, stacks: 2, targetSelf: true) }
            },
            "hero_spring_medic" => new PassiveData
            {
                Id = "awaken_spring_medic_healing_ward",
                Trigger = PassiveTrigger.OnHealDone,
                Applies = new[] { new StatusApplication(StatusId.DefUp, 1f, duration: 2, stacks: 1, targetSelf: true) }
            },

            // --- Trickster (OnPerfectCommand, SpdUp tự thân) ---
            "hero_night_stalker" => new PassiveData
            {
                Id = "awaken_night_stalker_shadow_step",
                Trigger = PassiveTrigger.OnPerfectCommand,
                Applies = new[] { new StatusApplication(StatusId.SpdUp, 1f, duration: 2, stacks: 2, targetSelf: true) }
            },
            "hero_spark_runner" => new PassiveData // Common — 1 stack thay vì 2
            {
                Id = "awaken_spark_runner_static_step",
                Trigger = PassiveTrigger.OnPerfectCommand,
                Applies = new[] { new StatusApplication(StatusId.SpdUp, 1f, duration: 2, stacks: 1, targetSelf: true) }
            },
            "hero_mirage_fox" => new PassiveData // Legendary — 3 stack, mạnh nhất
            {
                Id = "awaken_mirage_fox_prismatic_step",
                Trigger = PassiveTrigger.OnPerfectCommand,
                Applies = new[] { new StatusApplication(StatusId.SpdUp, 1f, duration: 2, stacks: 3, targetSelf: true) }
            },

            // --- Summoner (OnAllyDeath, AtkUp tự thân) ---
            "hero_beast_tamer" => new PassiveData
            {
                Id = "awaken_beast_tamer_wild_pact",
                Trigger = PassiveTrigger.OnAllyDeath,
                Applies = new[] { new StatusApplication(StatusId.AtkUp, 1f, duration: 3, stacks: 2, targetSelf: true) }
            },
            "hero_flame_binder" => new PassiveData
            {
                Id = "awaken_flame_binder_infernal_pact",
                Trigger = PassiveTrigger.OnAllyDeath,
                Applies = new[] { new StatusApplication(StatusId.AtkUp, 1f, duration: 3, stacks: 2, targetSelf: true) }
            },
            "hero_star_weaver" => new PassiveData
            {
                Id = "awaken_star_weaver_astral_pact",
                Trigger = PassiveTrigger.OnAllyDeath,
                Applies = new[] { new StatusApplication(StatusId.AtkUp, 1f, duration: 3, stacks: 2, targetSelf: true) }
            },

            _ => null
        };
    }
}
