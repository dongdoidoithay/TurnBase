#!/usr/bin/env python3
"""
import_hero_frames.py — copy 1 bộ frame rig (clean/{hero}_rig/{state}/*.png) vào
Assets/_Project/Resources/Art/Characters/Heroes/{hero}/Animations/, đổi tên đúng quy ước
UnitView.LoadFrames ({hero}_{state}_{NN}.png), viết .meta tay (Point filter/Compression None/
PPU32/pivot bottom-center) — tách thành script riêng vì đã lặp lại y hệt 3 lần
(hero_ember_knight/hero_frost_sage/hero_gale_thief), tránh gõ tay lại + rủi ro gõ sai.

Dùng:
    python3 import_hero_frames.py --hero hero_gale_thief \
        --src Tools/pixel-art-pipeline/clean/hero_gale_thief_rig \
        --dst Assets/_Project/Resources/Art/Characters/Heroes/hero_gale_thief/Animations
"""

import argparse
import os
import shutil
import uuid

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
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
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
  alignment: 9
  spritePivot: {{x: 0.5, y: 0}}
  spritePixelsToUnits: 32
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
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
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: 4
    textureCompression: 0
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
      alignment: 9
      pivot: {{x: 0.5, y: 0}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      customData:
      outline: []
      physicsShape: []
      tessellationDetail: -1
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
"""

STATES = ("idle", "attack", "move", "damage", "die")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--hero", required=True, help="defId, vd hero_gale_thief")
    ap.add_argument("--src", required=True, help="thư mục clean/{hero}_rig gốc")
    ap.add_argument("--dst", required=True, help="Animations/ đích trong Assets/Resources")
    args = ap.parse_args()

    os.makedirs(args.dst, exist_ok=True)
    count = 0
    for state in STATES:
        state_dir = os.path.join(args.src, state)
        if not os.path.isdir(state_dir):
            continue
        for i, fname in enumerate(sorted(os.listdir(state_dir))):
            dst_name = f"{args.hero}_{state}_{i:02d}.png"
            shutil.copy(os.path.join(state_dir, fname), os.path.join(args.dst, dst_name))
            name = dst_name[:-4]
            content = META.format(
                guid=uuid.uuid4().hex, name=name, internal_id=uuid.uuid4().int % (2**63),
                w=32, h=32, sprite_id=uuid.uuid4().hex[:32], sheet_sprite_id=uuid.uuid4().hex[:32],
            )
            with open(os.path.join(args.dst, dst_name + ".meta"), "w") as f:
                f.write(content)
            count += 1

    print(f"imported {count} files -> {args.dst}")


if __name__ == "__main__":
    main()
