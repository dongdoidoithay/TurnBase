#!/bin/bash
# gen_all_enemies.sh — Batch generate tất cả 66 enemy/boss frames vào clean/enemy_*/
# Dùng: bash gen_all_enemies.sh [--skip-existing]
#
# Output: clean/{enemy_id}_rig/{state}/*.png (5 state × 66 enemy)
# Sau đó chạy import_enemy_frames.py để copy vào Assets/

set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
CLEAN_DIR="$SCRIPT_DIR/clean"
SKIP=${1:-""}

run_kit() {
    local enemy_id="$1"
    local kit="$2"
    local out_base="$CLEAN_DIR/${enemy_id}_rig"

    if [ "$SKIP" = "--skip-existing" ] && [ -d "$out_base/die" ]; then
        echo "  SKIP $enemy_id (already exists)"
        return
    fi
    echo "  GEN  $enemy_id  [$kit]"
    python3 "$SCRIPT_DIR/enemy_rig.py" --kit "$kit" --enemy "$enemy_id" \
        --all-states --out "$out_base"
}

echo "=== gen_all_enemies.sh — $(date) ==="
echo "Output: $CLEAN_DIR"
echo ""

# ---- Chapter 1 ----
run_kit enemy_goblin           goblin
run_kit enemy_goblin_archer    goblin_wind
run_kit enemy_goblin_shaman    goblin_dark
run_kit enemy_slime            slime_water
run_kit enemy_wolf             wolf_neutral
run_kit enemy_bat              bat_dark
run_kit enemy_bomb_slime       slime_fire
run_kit enemy_ogre_brute       brute_earth
run_kit enemy_shield_bearer    knight_earth
run_kit enemy_skeleton         skeleton
run_kit boss_alpha_wolf        wolf_neutral

# ---- Chapter 2 ----
run_kit boss_goblin_king       goblin_king
run_kit enemy_bog_zombie       zombie
run_kit enemy_swamp_troll      brute_water
run_kit enemy_will_o_wisp      wisp_neutral
run_kit enemy_giant_leech      serpent_water
run_kit enemy_venomous_spider  spider_neutral
run_kit enemy_bog_witch        caster_dark
run_kit enemy_mud_crawler      golem_water
run_kit enemy_mire_serpent     serpent_water
run_kit enemy_poison_toad      toad_water
run_kit enemy_swamp_rat_swarm  swarm_neutral

# ---- Chapter 3 ----
run_kit boss_lich              boss_lich
run_kit enemy_bone_swordsman   skeleton
run_kit enemy_bone_archer      skeleton
run_kit enemy_crypt_wraith     wisp_dark
run_kit enemy_crypt_spider     spider_dark
run_kit enemy_grave_golem      golem_earth
run_kit enemy_mummy_guardian   mummy
run_kit enemy_death_priest     caster_undead
run_kit enemy_cursed_gargoyle  brute_dark
run_kit enemy_necrotic_hound   wolf_dark
run_kit enemy_soul_reaper      caster_undead
run_kit enemy_phantom_knight   knight_dark

# ---- Chapter 4 ----
run_kit boss_magma_drake       boss_drake
run_kit enemy_fire_imp         bat_fire
run_kit enemy_magma_hound      wolf_fire
run_kit enemy_lava_slime       slime_fire
run_kit enemy_flame_wisp       wisp_fire
run_kit enemy_ember_knight     brute_fire
run_kit enemy_pyroclast_mage   caster_fire
run_kit enemy_molten_brute     brute_fire
run_kit enemy_volcanic_crab    golem_earth
run_kit enemy_flame_serpent    serpent_fire
run_kit enemy_obsidian_golem   golem_earth
run_kit enemy_cinder_bat       bat_fire
run_kit enemy_charred_zombie   zombie_fire

# ---- Chapter 5 (Dark/Void) ----
run_kit boss_void_king         boss_void
run_kit boss_trial_champion    boss_void
run_kit enemy_void_cultist     caster_dark
run_kit enemy_shadow_stalker   wolf_dark
run_kit enemy_abyssal_wraith   wisp_dark
run_kit enemy_shadow_knight    knight_dark2
run_kit enemy_void_reaver      brute_dark
run_kit enemy_chaos_spawn      horror_dark
run_kit enemy_dark_sentinel    knight_dark2
run_kit enemy_nether_hound     wolf_dark
run_kit enemy_corrupted_golem  golem_dark
run_kit enemy_void_serpent     serpent_dark
run_kit enemy_nightmare_fiend  horror_dark
run_kit enemy_ash_wraith       wisp_dark
run_kit enemy_fungal_horror    horror_neutral
run_kit enemy_void_horror      horror_dark
run_kit enemy_drowned_knight   knight_water
run_kit enemy_star_priest      caster_light
run_kit enemy_abyss_stalker    wolf_dark

echo ""
echo "=== DONE ==="
total=$(find "$CLEAN_DIR" -name "*_rig_*_??.png" | wc -l)
echo "Tổng frames: $total"
