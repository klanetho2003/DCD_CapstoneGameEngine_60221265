using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Capstone.Fireworks
{
    public enum ImageSampling { Auto, ColorAndAlpha, BrightnessMask }

    public sealed class ImageFireworkPattern
    {
        private const int GridLimit = 96;
        private const float Span = 5.8f;
        private const float Lifetime = 2.7f;
        private const float Formation = .32f;

        private readonly List<ParticleSystem.Particle> points;
        private ImageFireworkPattern(List<ParticleSystem.Particle> points) => this.points = points;

        public static ImageFireworkPattern Sample(Texture2D image, ImageSampling mode)
        {
            if (!image) return null;
            float ratio = (float)GridLimit / Mathf.Max(image.width, image.height);
            int width = Mathf.Max(1, Mathf.RoundToInt(image.width * ratio));
            int height = Mathf.Max(1, Mathf.RoundToInt(image.height * ratio));
            Color32[] pixels = ReadGrid(image, width, height);
            bool alphaOnly = image.format == TextureFormat.Alpha8;
            bool singleRed = GraphicsFormatUtility.GetComponentCount(image.graphicsFormat) == 1 && !alphaOnly;
            bool useLuma = mode == ImageSampling.BrightnessMask ||
                (mode == ImageSampling.Auto && (singleRed || IsOpaqueGrayscale(pixels)));
            float step = Span / Mathf.Max(width, height);
            var points = new List<ParticleSystem.Particle>(pixels.Length);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                Color32 c = pixels[index];
                if (singleRed) c.g = c.b = c.r;
                if (alphaOnly) c.r = c.g = c.b = 255;
                if (useLuma)
                {
                    // Brightness is coverage for a mask. Preserve hue for a coloured mask.
                    float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                    float luma = (.2126f * c.r + .7152f * c.g + .0722f * c.b) / 255f;
                    c.a = (byte)Mathf.RoundToInt(c.a * luma);
                    if (peak > 0f)
                    {
                        c.r = (byte)Mathf.RoundToInt(c.r * 255f / peak);
                        c.g = (byte)Mathf.RoundToInt(c.g * 255f / peak);
                        c.b = (byte)Mathf.RoundToInt(c.b * 255f / peak);
                    }
                }
                if (c.a < 6) continue;
                Vector3 target = new Vector3(x + .5f - width * .5f, y + .5f - height * .5f) * step;
                points.Add(new ParticleSystem.Particle
                {
                    velocity = target,
                    startColor = c,
                    startSize = step * 1.65f,
                    startLifetime = Lifetime,
                    remainingLifetime = Lifetime,
                    randomSeed = (uint)(index + 1)
                });
            }
            return points.Count == 0 ? null : new ImageFireworkPattern(points);
        }

        private static bool IsOpaqueGrayscale(Color32[] pixels)
        {
            foreach (var c in pixels)
            {
                if (c.a < 250 || Mathf.Max(c.r, Mathf.Max(c.g, c.b)) -
                    Mathf.Min(c.r, Mathf.Min(c.g, c.b)) > 3) return false;
            }
            return true;
        }

        private static Color32[] ReadGrid(Texture2D image, int width, int height)
        {
            // The small GPU copy also works with compressed and non-readable imported textures.
            var previous = RenderTexture.active;
            bool previousWrite = GL.sRGBWrite;
            bool srgb = image.isDataSRGB && QualitySettings.activeColorSpace == ColorSpace.Linear;
            var buffer = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
                srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            Texture2D copy = null;
            try
            {
                GL.sRGBWrite = srgb;
                Graphics.Blit(image, buffer);
                RenderTexture.active = buffer;
                copy = new Texture2D(width, height, TextureFormat.RGBA32, false, !srgb);
                copy.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                return copy.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                GL.sRGBWrite = previousWrite;
                RenderTexture.ReleaseTemporary(buffer);
                if (copy) Object.Destroy(copy);
            }
        }

        public void Play(ParticleSystem system, Vector3 origin, float scale)
        {
            var main = system.main;
            main.gravityModifier = 0f;
            main.maxParticles = points.Count;
            var trails = system.trails;
            trails.enabled = false;
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            // Integral of this triangular speed curve is one world-space target offset.
            float initialSpeed = 2f / (Formation * Lifetime);
            float slope = -initialSpeed / Formation;
            var brake = new AnimationCurve(new Keyframe(0f, initialSpeed, 0f, slope),
                new Keyframe(Formation, 0f, slope, 0f), new Keyframe(1f, 0f, 0f, 0f));
            velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f, brake);
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .70f),
                        new GradientAlphaKey(0f, 1f) });
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = fade;
            var batch = points.ToArray();
            for (int i = 0; i < batch.Length; i++)
            {
                batch[i].position = origin;
                batch[i].velocity *= scale;
                batch[i].startSize *= scale;
            }
            system.Play(false);
            system.SetParticles(batch, batch.Length);
        }
    }
}
