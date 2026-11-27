using System.Collections;
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

        private struct Pixel
        {
            public Vector3 target;
            public Color32 color;
        }

        private struct Flight
        {
            public Vector3 start, end;
            public float delay, travelTime, fadeStart, lifetime;
        }

        private readonly Pixel[] pixels;
        private readonly float diameter;
        private ImageFireworkPattern(Pixel[] pixels, float diameter)
        {
            this.pixels = pixels;
            this.diameter = diameter;
        }

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
            var points = new List<Pixel>(pixels.Length);
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
                points.Add(new Pixel { target = target, color = c });
            }
            int particleCount = points.Count / 2;
            if (particleCount == 0) return null;

            // Choose one visible pixel from each pair across the whole image.
            // A fixed seed preserves the same details when the show is replayed.
            var selected = new Pixel[particleCount];
            var selection = new System.Random(37);
            for (int i = 0; i < selected.Length; i++)
                selected[i] = points[2 * i + selection.Next(2)];
            return new ImageFireworkPattern(selected, step * 1.65f);
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
                if (copy)
                {
                    if (Application.isPlaying) Object.Destroy(copy);
                    else Object.DestroyImmediate(copy);
                }
            }
        }

        public IEnumerator Play(ParticleSystem system, Vector3 origin, float scale, System.Random random)
        {
            var main = system.main;
            main.gravityModifier = 0f;
            main.maxParticles = pixels.Length;
            var trails = system.trails;
            trails.enabled = false;
            var velocity = system.velocityOverLifetime;
            velocity.enabled = false;
            var color = system.colorOverLifetime;
            color.enabled = false;

            var flights = new Flight[pixels.Length];
            var batch = new ParticleSystem.Particle[pixels.Length];
            for (int i = 0; i < batch.Length; i++)
            {
                Flight flight = MakeFlight(pixels[i].target, scale, random);
                flights[i] = flight;
                batch[i] = new ParticleSystem.Particle
                {
                    position = origin + flight.start,
                    startColor = pixels[i].color,
                    startSize = diameter * scale,
                    startLifetime = flight.lifetime,
                    remainingLifetime = flight.lifetime,
                    randomSeed = (uint)(i + 1)
                };
            }
            system.Play(false);
            system.SetParticles(batch, batch.Length);
            while (true)
            {
                yield return null;
                int alive = system.GetParticles(batch);
                if (alive == 0) yield break;
                for (int i = 0; i < alive; i++)
                {
                    // Shuriken can reorder survivors when another spark dies.
                    int id = (int)batch[i].randomSeed - 1;
                    Flight flight = flights[id];
                    float age = flight.lifetime - batch[i].remainingLifetime;
                    float time = Mathf.Clamp01((age - flight.delay) / flight.travelTime);
                    float remaining = 1f - time;
                    float easeOutCubic = 1f - remaining * remaining * remaining;
                    batch[i].position = origin + Vector3.Lerp(flight.start, flight.end, easeOutCubic);
                    if (age > flight.fadeStart)
                    {
                        float opacity = 1f - Mathf.SmoothStep(0f, 1f,
                            (age - flight.fadeStart) / (flight.lifetime - flight.fadeStart));
                        Color32 tint = pixels[id].color;
                        tint.a = (byte)Mathf.RoundToInt(tint.a * opacity);
                        batch[i].startColor = tint;
                    }
                }
                system.SetParticles(batch, alive);
            }
        }

        private static Flight MakeFlight(Vector3 pixel, float scale, System.Random random)
        {
            float Range(float low, float high) => Mathf.Lerp(low, high, (float)random.NextDouble());
            Vector3 Radial(float radius)
            {
                float angle = Range(0f, Mathf.PI * 2f);
                return new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), Range(-.13f,.13f) * radius);
            }
            float delay = Range(0f, .16f);
            float travel = Range(.94f, 1.28f);
            float fade = delay + travel + Range(.1f, .25f);
            return new Flight
            {
                start = Radial(.13f * Mathf.Sqrt(Range(0f, 1f))) * scale,
                end = pixel * scale,
                delay = delay, travelTime = travel, fadeStart = fade,
                lifetime = fade + Range(.70f, 1.16f)
            };
        }
    }
}
