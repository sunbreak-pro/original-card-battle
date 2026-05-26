import { Assets, type Texture } from "pixi.js";

/**
 * Texture loading and caching manager.
 * Phase 1: Basic structure only.
 * Phase 3: Spritesheet loading, atlas management, bundle preloading.
 */
const textureCache = new Map<string, Texture>();

export async function loadTexture(key: string, url: string): Promise<Texture> {
  const cached = textureCache.get(key);
  if (cached) return cached;

  const texture = await Assets.load<Texture>(url);
  textureCache.set(key, texture);
  return texture;
}

export function getTexture(key: string): Texture | undefined {
  return textureCache.get(key);
}

export function destroyAllTextures(): void {
  for (const texture of textureCache.values()) {
    texture.destroy();
  }
  textureCache.clear();
}
