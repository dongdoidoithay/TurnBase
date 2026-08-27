#!/usr/bin/env python3
"""Import batch 3 — chrome cat tu chinh anh mau that (Screen_combat.jpg), thay the card_gold*
cu (ve tay compose.py) bang ban cat that 100% giong mau. Cung template .meta nhu 2 batch truoc."""
import shutil
import uuid
from pathlib import Path
from PIL import Image

SRC = Path("/Users/hainx/__Data/__Unity/__2D/TurnBase/Tools/pixel-art-pipeline/clean/ui_chrome_sample")
DST = Path("/Users/hainx/__Data/__Unity/__2D/TurnBase/Assets/_Project/Resources/Art/UI/Chrome")

# (src filename, dst name (no ext), border L,B,R,T)
FILES = [
    ("card_normal.png", "card_gold", (9, 9, 9, 14)),
    ("card_selected.png", "card_gold_selected", (9, 9, 9, 14)),
    ("card_disabled.png", "card_gold_disabled", (9, 9, 9, 14)),
    ("icon_slot.png", "icon_slot_brown", (5, 6, 5, 6)),
]

META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable:
  - first:
      213: -6792013190358773584
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
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: {bl}, y: {bb}, z: {br}, w: {bt}}}
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
    textureFormat: -1
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
      alignment: 0
      pivot: {{x: 0, y: 0}}
      border: {{x: {bl}, y: {bb}, z: {br}, w: {bt}}}
      customData:
      outline: []
      physicsShape: []
      tessellationDetail: -1
      bones: []
      spriteID: 0b0d5ee2dfbedb1a0800000000000000
      internalID: -6792013190358773584
      vertices: []
      indices:
      edges: []
      weights: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable:
      {name}_0: -6792013190358773584
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

DST.mkdir(parents=True, exist_ok=True)
for rel, name, border in FILES:
    src_path = SRC / rel
    dst_png = DST / f"{name}.png"
    shutil.copy(src_path, dst_png)
    w, h = Image.open(src_path).size
    guid = uuid.uuid4().hex
    meta = META_TEMPLATE.format(guid=guid, name=name, w=w, h=h,
                                 bl=border[0], bb=border[1], br=border[2], bt=border[3])
    (DST / f"{name}.png.meta").write_text(meta)
    print(f"  imported {name}.png ({w}x{h}) border={border}")
