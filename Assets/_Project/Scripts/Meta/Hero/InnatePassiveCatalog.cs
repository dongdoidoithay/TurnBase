using Game.Combat.Model;
using Game.Data;

namespace Game.Meta.Hero
{
    /// <summary>
    /// Passive BẨM SINH mỗi hero (task-innate-passive.md) — <see cref="CombatUnit.Passive"/>,
    /// KHÁC <see cref="CombatUnit.Awakening"/> (chỉ có ★6): có ngay từ ★1, không điều kiện sao.
    /// Cùng lý do hard-code như <see cref="AwakeningCatalog"/> (StatModifier/StatusApplication có
    /// field readonly, Unity Inspector không serialize được).
    ///
    /// Ưu tiên dùng 4 trigger chưa Awakening nào dùng (OnTurnStart/OnDamageTaken/
    /// OnHpBelowThreshold/OnBreakTriggered — task-ascend.md §10 audit phát hiện có hook thật
    /// nhưng chưa nội dung nào exercise) để passive bẩm sinh CẢM GIÁC KHÁC Awakening, không trùng
    /// cơ chế. V1 — số liệu placeholder chờ playtest, giống style AwakeningCatalog.
    ///
    /// Lưu ý kỹ thuật: <see cref="StatusApplication.Chance"/> CHỈ áp dụng cho debuff — buff lên
    /// đồng minh/bản thân luôn thành công 100% (xem StatusProcessor.Apply) — nên mọi Applies ở
    /// đây là buff tự thân đều kích hoạt chắc chắn mỗi lần trigger nổ, không có khái niệm "% cơ
    /// hội proc" (PassiveData không có field trigger-chance riêng, chỉ Chance của từng status).
    /// </summary>
    public static class InnatePassiveCatalog
    {
        public static PassiveData Get(string heroDefId) => heroDefId switch
        {
            // Vanguard/Fire — càng bị đánh càng cứng, phản ứng mỗi lần chịu damage.
            "hero_ember_knight" => new PassiveData
            {
                Id = "innate_ember_knight_iron_resolve",
                Trigger = PassiveTrigger.OnDamageTaken,
                Applies = new[]
                {
                    new StatusApplication(StatusId.DefUp, 1f, duration: 1, stacks: 1, targetSelf: true),
                }
            },

            // Slayer/Dark — nhanh nhẹn sẵn có mỗi đầu lượt, khác Awakening (chỉ nổ khi giết).
            "hero_shadow_fang" => new PassiveData
            {
                Id = "innate_shadow_fang_quick_reflexes",
                Trigger = PassiveTrigger.OnTurnStart,
                Applies = new[]
                {
                    new StatusApplication(StatusId.SpdUp, 1f, duration: 1, stacks: 1, targetSelf: true),
                }
            },

            // Arcanist/Water — lá chắn khẩn cấp khi máu thấp, dùng đúng trigger còn bỏ trống.
            "hero_frost_sage" => new PassiveData
            {
                Id = "innate_frost_sage_frost_ward",
                Trigger = PassiveTrigger.OnHpBelowThreshold,
                Threshold = 0.3f,
                Applies = new[]
                {
                    new StatusApplication(StatusId.DefUp, 1f, duration: 2, stacks: 1, targetSelf: true),
                }
            },

            // Warden/Light — kiên cường kể cả khi bị Break, dùng đúng trigger còn bỏ trống.
            "hero_dawn_cleric" => new PassiveData
            {
                Id = "innate_dawn_cleric_unbreakable_faith",
                Trigger = PassiveTrigger.OnBreakTriggered,
                Applies = new[]
                {
                    new StatusApplication(StatusId.Regen, 1f, duration: 2, stacks: 1, targetSelf: true),
                }
            },

            // Trickster/Wind — bẩm sinh nhanh nhẹn vĩnh viễn, nhẹ hơn hẳn Awakening.
            "hero_gale_thief" => new PassiveData
            {
                Id = "innate_gale_thief_windborn",
                Trigger = PassiveTrigger.OnBattleStart,
                Modifiers = new[]
                {
                    new StatModifier(StatType.SpdPct, 8f, "innate"),
                }
            },

            // Summoner/Dark — cộng dồn với Awakening (cùng OnKill) khi ★6, phần thưởng hợp lý cho
            // việc lên tối đa: yếu hơn Awakening (1 stack thay vì 2).
            "hero_bone_caller" => new PassiveData
            {
                Id = "innate_bone_caller_grave_whisper",
                Trigger = PassiveTrigger.OnKill,
                Applies = new[]
                {
                    new StatusApplication(StatusId.AtkUp, 1f, duration: 2, stacks: 1, targetSelf: true),
                }
            },

            // ============================================================
            // 18 hero mở rộng (task-hero-roster.md) — GIỮ ĐÚNG trigger + hình dạng hiệu ứng của
            // hero mẫu cùng class, đúng tinh thần AwakeningCatalog ở trên (class quyết định cơ
            // chế, rarity chỉnh nhẹ số liệu). Innate luôn NHẸ HƠN Awakening cùng hero (đúng ý
            // "bẩm sinh yếu hơn giác ngộ") — không đổi ở lượt này.
            // ============================================================

            // --- Vanguard (OnDamageTaken, DefUp tự thân mỗi lần chịu damage) ---
            "hero_iron_bastion" => new PassiveData
            {
                Id = "innate_iron_bastion_stalwart_guard",
                Trigger = PassiveTrigger.OnDamageTaken,
                Applies = new[] { new StatusApplication(StatusId.DefUp, 1f, duration: 1, stacks: 1, targetSelf: true) }
            },
            "hero_tide_warden" => new PassiveData
            {
                Id = "innate_tide_warden_flowing_defense",
                Trigger = PassiveTrigger.OnDamageTaken,
                Applies = new[] { new StatusApplication(StatusId.DefUp, 1f, duration: 1, stacks: 1, targetSelf: true) }
            },
            "hero_stormguard" => new PassiveData // Legendary — 2 stack thay vì 1
            {
                Id = "innate_stormguard_gale_barrier",
                Trigger = PassiveTrigger.OnDamageTaken,
                Applies = new[] { new StatusApplication(StatusId.DefUp, 1f, duration: 1, stacks: 2, targetSelf: true) }
            },

            // --- Slayer (OnTurnStart, SpdUp tự thân — phản xạ nhanh bẩm sinh) ---
            "hero_blade_dancer" => new PassiveData
            {
                Id = "innate_blade_dancer_swift_strike",
                Trigger = PassiveTrigger.OnTurnStart,
                Applies = new[] { new StatusApplication(StatusId.SpdUp, 1f, duration: 1, stacks: 1, targetSelf: true) }
            },
            "hero_crimson_reaver" => new PassiveData
            {
                Id = "innate_crimson_reaver_swift_strike",
                Trigger = PassiveTrigger.OnTurnStart,
                Applies = new[] { new StatusApplication(StatusId.SpdUp, 1f, duration: 1, stacks: 1, targetSelf: true) }
            },
            "hero_stone_breaker" => new PassiveData
            {
                Id = "innate_stone_breaker_swift_strike",
                Trigger = PassiveTrigger.OnTurnStart,
                Applies = new[] { new StatusApplication(StatusId.SpdUp, 1f, duration: 1, stacks: 1, targetSelf: true) }
            },

            // --- Arcanist (OnHpBelowThreshold 0.3, DefUp khẩn cấp) ---
            "hero_pyromancer" => new PassiveData
            {
                Id = "innate_pyromancer_arcane_ward",
                Trigger = PassiveTrigger.OnHpBelowThreshold,
                Threshold = 0.3f,
                Applies = new[] { new StatusApplication(StatusId.DefUp, 1f, duration: 2, stacks: 1, targetSelf: true) }
            },
            "hero_terra_seer" => new PassiveData
            {
                Id = "innate_terra_seer_arcane_ward",
                Trigger = PassiveTrigger.OnHpBelowThreshold,
                Threshold = 0.3f,
                Applies = new[] { new StatusApplication(StatusId.DefUp, 1f, duration: 2, stacks: 1, targetSelf: true) }
            },
            "hero_void_scholar" => new PassiveData // Legendary — 2 stack thay vì 1
            {
                Id = "innate_void_scholar_arcane_ward",
                Trigger = PassiveTrigger.OnHpBelowThreshold,
                Threshold = 0.3f,
                Applies = new[] { new StatusApplication(StatusId.DefUp, 1f, duration: 2, stacks: 2, targetSelf: true) }
            },

            // --- Warden (OnBreakTriggered, Regen tự thân) ---
            "hero_grove_keeper" => new PassiveData
            {
                Id = "innate_grove_keeper_resilient_spirit",
                Trigger = PassiveTrigger.OnBreakTriggered,
                Applies = new[] { new StatusApplication(StatusId.Regen, 1f, duration: 2, stacks: 1, targetSelf: true) }
            },
            "hero_moon_priestess" => new PassiveData // Legendary — 2 stack thay vì 1
            {
                Id = "innate_moon_priestess_resilient_spirit",
                Trigger = PassiveTrigger.OnBreakTriggered,
                Applies = new[] { new StatusApplication(StatusId.Regen, 1f, duration: 2, stacks: 2, targetSelf: true) }
            },
            "hero_spring_medic" => new PassiveData
            {
                Id = "innate_spring_medic_resilient_spirit",
                Trigger = PassiveTrigger.OnBreakTriggered,
                Applies = new[] { new StatusApplication(StatusId.Regen, 1f, duration: 2, stacks: 1, targetSelf: true) }
            },

            // --- Trickster (OnBattleStart, SPD% vĩnh viễn bẩm sinh) ---
            "hero_night_stalker" => new PassiveData
            {
                Id = "innate_night_stalker_swift_shadow",
                Trigger = PassiveTrigger.OnBattleStart,
                Modifiers = new[] { new StatModifier(StatType.SpdPct, 8f, "innate") }
            },
            "hero_spark_runner" => new PassiveData
            {
                Id = "innate_spark_runner_swift_shadow",
                Trigger = PassiveTrigger.OnBattleStart,
                Modifiers = new[] { new StatModifier(StatType.SpdPct, 8f, "innate") }
            },
            "hero_mirage_fox" => new PassiveData // Legendary — +10% thay vì +8%
            {
                Id = "innate_mirage_fox_swift_shadow",
                Trigger = PassiveTrigger.OnBattleStart,
                Modifiers = new[] { new StatModifier(StatType.SpdPct, 10f, "innate") }
            },

            // --- Summoner (OnKill, AtkUp tự thân — nhẹ hơn Awakening cùng hero) ---
            "hero_beast_tamer" => new PassiveData
            {
                Id = "innate_beast_tamer_primal_whisper",
                Trigger = PassiveTrigger.OnKill,
                Applies = new[] { new StatusApplication(StatusId.AtkUp, 1f, duration: 2, stacks: 1, targetSelf: true) }
            },
            "hero_flame_binder" => new PassiveData
            {
                Id = "innate_flame_binder_primal_whisper",
                Trigger = PassiveTrigger.OnKill,
                Applies = new[] { new StatusApplication(StatusId.AtkUp, 1f, duration: 2, stacks: 1, targetSelf: true) }
            },
            "hero_star_weaver" => new PassiveData
            {
                Id = "innate_star_weaver_primal_whisper",
                Trigger = PassiveTrigger.OnKill,
                Applies = new[] { new StatusApplication(StatusId.AtkUp, 1f, duration: 2, stacks: 1, targetSelf: true) }
            },

            _ => null
        };
    }
}
