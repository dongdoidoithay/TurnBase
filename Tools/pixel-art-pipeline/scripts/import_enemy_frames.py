#!/usr/bin/env python3
"""
import_enemy_frames.py — copy frames rig từ clean/{enemy}_rig/ vào
Assets/_Project/Resources/Art/Characters/Enemies/{enemy}/Animations/,
đổi tên đúng quy ước UnitView.LoadFrames ({enemy}_{state}_{NN}.png),
viết .meta (Point filter / Compression None / PPU 32 / pivot bottom-center).

Dùng single enemy:
    python3 import_enemy_frames.py --enemy enemy_goblin \
        --src Tools/pixel-art-pipeline/clean/enemy_goblin_rig \
        --dst Assets/_Project/Resources/Art/Characters/Enemies/enemy_goblin/Animations

Dùng batch tất cả (chạy sau gen_all_enemies.sh):
    python3 import_enemy_frames.py --batch \
        --src-root Tools/pixel-art-pipeline/clean \
        --dst-root Assets/_Project/Resources/Art/Characters/Enemies
"""

import argparse
import os
import shutil
import uuid

# Cùng META template với import_hero_frames.py
META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable:
  - first:
      213: {internal_id}
    second: {name}_0
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  enableMipMap: 0
  sRGBTexture: 1
  alphaSource: 1
  alphaIsTransparency: 1
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 64
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 2
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites:
    - serializedVersion: 2
      name: {name}_0
      rect:
        serializedVersion: 2
        x: 0
        y: 0
        width: {w}
        height: {h}
      alignment: 7
      pivot:
        x: 0.5
        y: 0
      border:
        x: 0
        y: 0
        z: 0
        w: 0
      outline: []
      physicsShape: []
      tessellationDetail: 0
      bones: []
      spriteID: {sprite_id}
      internalID: {internal_id}
      vertices: []
      indices:
      edges: []
      weights: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID: {sheet_sprite_id}
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable:
      {name}_0: {internal_id}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
  filterMode: 0
  aniso: 1
  mipBias: 0
  wrapU: 1
  wrapV: 1
  wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 7
  spritePivot:
    x: 0.5
    y: 0
  spritePixelsToUnits: 32
  spriteBorder:
    x: 0
    y: 0
    z: 0
    w: 0
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  cookieLightType: 0
"""

STATES = ("idle", "attack", "move", "damage", "die")


def import_one(enemy_id, src_rig_dir, dst_anim_dir):
    os.makedirs(dst_anim_dir, exist_ok=True)
    count = 0
    for state in STATES:
        state_dir = os.path.join(src_rig_dir, state)
        if not os.path.isdir(state_dir):
            print(f"  [WARN] {enemy_id}/{state}: thư mục không tồn tại, bỏ qua")
            continue
        pngs = sorted(f for f in os.listdir(state_dir) if f.endswith(".png"))
        for i, fname in enumerate(pngs):
            dst_name = f"{enemy_id}_{state}_{i:02d}.png"
            shutil.copy(os.path.join(state_dir, fname), os.path.join(dst_anim_dir, dst_name))
            name = dst_name[:-4]
            meta_content = META.format(
                guid=uuid.uuid4().hex,
                name=name,
                internal_id=uuid.uuid4().int % (2**63),
                w=32, h=32,
                sprite_id=uuid.uuid4().hex[:32],
                sheet_sprite_id=uuid.uuid4().hex[:32],
            )
            with open(os.path.join(dst_anim_dir, dst_name + ".meta"), "w") as f:
                f.write(meta_content)
            count += 1
    return count


def main():
    ap = argparse.ArgumentParser(description="Import enemy frames vào Unity Assets")
    ap.add_argument("--enemy",    help="defId đơn lẻ, vd enemy_goblin")
    ap.add_argument("--src",      help="clean/{enemy}_rig/ source dir (chế độ đơn lẻ)")
    ap.add_argument("--dst",      help="Animations/ đích (chế độ đơn lẻ)")
    ap.add_argument("--batch",    action="store_true", help="Import tất cả enemy một lượt")
    ap.add_argument("--src-root", help="clean/ root dir (chế độ batch)")
    ap.add_argument("--dst-root", help="Enemies/ root dir (chế độ batch)")
    args = ap.parse_args()

    if args.batch:
        if not args.src_root or not args.dst_root:
            ap.error("--batch cần --src-root và --dst-root")
        total = 0
        import os as _os
        src_root = args.src_root
        dst_root = args.dst_root
        rig_dirs = sorted(
            d for d in _os.listdir(src_root)
            if d.endswith("_rig") and _os.path.isdir(_os.path.join(src_root, d))
        )
        for rig_dir_name in rig_dirs:
            enemy_id = rig_dir_name[:-4]  # strip _rig suffix
            src_rig = _os.path.join(src_root, rig_dir_name)
            dst_anim = _os.path.join(dst_root, enemy_id, "Animations")
            n = import_one(enemy_id, src_rig, dst_anim)
            print(f"  {enemy_id}: {n} files -> {dst_anim}")
            total += n
        print(f"\nTổng: {total} files imported")
        return

    if not args.enemy or not args.src or not args.dst:
        ap.print_help()
        return

    n = import_one(args.enemy, args.src, args.dst)
    print(f"Imported {n} files -> {args.dst}")


if __name__ == "__main__":
    main()
