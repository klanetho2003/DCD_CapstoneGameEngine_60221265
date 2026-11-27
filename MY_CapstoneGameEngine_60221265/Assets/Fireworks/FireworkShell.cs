using System;
using System.Collections;
using UnityEngine;

namespace Capstone.Fireworks
{
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class FireworkShell : MonoBehaviour
    {
        [Serializable]
        private struct BurstLook
        {
            public Material particles;
            public Material trails;
        }

        [SerializeField] private RisingFirework rise;
        [SerializeField] private BurstLook heartLook;
        [SerializeField] private BurstLook saturnLook;
        [SerializeField] private BurstLook pinwheelLook;
        [SerializeField] private BurstLook clusterLook;
        [SerializeField] private Material imageSparkMaterial;
        private ImageFireworkPattern imagePattern;
        private ParticleSystem bloom;
        private FireworkCue cue;
        private System.Random random;
        private float scale;
        private Action<FireworkShell> finished;
        private bool bursting;

        public void Launch(FireworkCue shot, Vector3 start, Vector3 end, float size, int seed,
            Action<FireworkShell> onFinished, ImageFireworkPattern pattern = null)
        {
            cue = shot;
            imagePattern = pattern;
            scale = size;
            finished = onFinished;
            random = new System.Random(seed);
            bloom = GetComponent<ParticleSystem>();
            transform.position = start;
            rise.Ignite(this, start, end, shot.flightTime, size);
        }

        public void BurstAt(Vector3 origin)
        {
            if (bursting || finished == null) return;
            bursting = true;
            transform.position = origin;
            var main = bloom.main;
            main.gravityModifier = (cue.shape == BloomShape.Willow ? 0.29f : 0.095f) * scale;
            switch (cue.shape)
            {
                case BloomShape.Image when imagePattern != null:
                    bloom.GetComponent<ParticleSystemRenderer>().sharedMaterial = imageSparkMaterial;
                    StartCoroutine(imagePattern.Play(bloom, origin, scale, random));
                    return;
                case BloomShape.Heart:
                    BeginPattern(heartLook, .09f);
                    EmitHeart();
                    break;
                case BloomShape.Saturn:
                    BeginPattern(saturnLook, .065f);
                    EmitSaturn();
                    break;
                case BloomShape.Pinwheel:
                    BeginPattern(pinwheelLook, .28f, 1.3f);
                    StartCoroutine(SpinRibbons());
                    break;
                case BloomShape.StarCluster:
                    BeginPattern(clusterLook, .24f, 1.25f);
                    StartCoroutine(SplitStars());
                    break;
                default:
                    bloom.Play(false);
                    EmitClassic();
                    break;
            }
            Emit(Vector3.zero, 0.15f, 0.85f, Color.Lerp(cue.petals, Color.white, 0.72f));
        }

        private void BeginPattern(BurstLook look, float trailLifetime, float emissionWindow = .12f)
        {
            var renderer = bloom.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = look.particles;
            renderer.trailMaterial = look.trails;
            var main = bloom.main;
            main.duration = emissionWindow;
            main.gravityModifier = 0;
            main.maxParticles = 320;
            var velocity = bloom.velocityOverLifetime;
            velocity.speedModifier = 1f;
            var trails = bloom.trails;
            trails.lifetime = trailLifetime;
            bloom.Play(false);
        }

        private void EmitClassic()
        {
            bool ring = cue.shape == BloomShape.Ring;
            int petals = ring ? 76 : 124;
            float phase = Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < petals; i++)
            {
                // A Fibonacci sphere keeps the shell round while leaving air between trails.
                float angle = phase + (ring ? 2f * Mathf.PI * i / petals : 2.399963f * i);
                float z = ring ? 0f : 1f - 2f * (i + 0.5f) / petals;
                float radius = Mathf.Sqrt(1f - z * z);
                Vector3 spoke = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z);
                float speed = ring ? Range(2.25f, 2.36f) : Range(1.9f, 2.65f);
                Color petal = Color.Lerp(cue.petals, cue.tips, i % 5 == 0 ? 0.85f : Range(0f, 0.28f));
                float life = cue.shape == BloomShape.Willow ? Range(1.65f, 2.3f) : Range(1.18f, 1.75f);
                Emit(spoke * speed, life, Range(0.071f, 0.12f), petal);
            }

            for (int i = 0; i < 36; i++)
            {
                float angle = Range(0f, 2f * Mathf.PI);
                Vector3 spoke = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                Emit(spoke * Range(0.22f, 1.35f), Range(0.35f, 0.90f),
                    Range(0.045f, 0.09f), Color.Lerp(cue.tips, Color.white, 0.38f));
            }
        }

        private void EmitHeart()
        {
            const int samples = 112;
            for (int i = 0; i < samples; i++)
            {
                float t = i * (2f * Mathf.PI / samples);
                float sine = Mathf.Sin(t);
                float x = 16f * sine * sine * sine;
                float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f*t)
                    - 2f * Mathf.Cos(3f*t) - Mathf.Cos(4f*t) + 2f;
                Vector3 outline = new Vector3(x, y) / 16f;
                Color outer = Color.Lerp(cue.petals, cue.tips, .22f + .2f * Mathf.Cos(t));
                Emit(outline * 1.86f, Range(1.43f, 1.60f), Range(.105f, .14f), outer);
                if (i % 2 == 0)
                    Emit(outline * 1.42f, Range(1.25f, 1.43f), .09f, cue.tips);
            }
        }

        private void EmitSaturn()
        {
            const int globeSparks = 92;
            for (int i = 0; i < globeSparks; i++)
            {
                float z = 1f - 2f * (i + .5f) / globeSparks;
                float r = Mathf.Sqrt(1f - z*z);
                float a = i * 2.399963f;
                var point = new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), z);
                Emit(point * .77f, Range(1.38f, 1.57f), Range(.10f, .14f), cue.petals);
            }

            var tilt = Quaternion.AngleAxis(21f, Vector3.forward);
            for (int ring = 0; ring < 2; ring++)
            {
                float radius = ring == 0 ? 2.05f : 1.76f;
                for (int i = 0; i < 96; i++)
                {
                    float a = (i + .5f * ring) * 2f * Mathf.PI / 96f;
                    var orbit = new Vector3(Mathf.Cos(a), .30f * Mathf.Sin(a), .62f * Mathf.Sin(a));
                    var color = Color.Lerp(cue.tips, cue.petals, ring == 0 ? .1f : .48f);
                    Emit(tilt * orbit * radius, Range(1.45f, 1.65f), ring == 0 ? .12f : .077f, color);
                }
            }
        }

        private IEnumerator SpinRibbons()
        {
            // Changing the emission angle while earlier sparks travel outward draws four spirals.
            const int steps = 37;
            float startTime = Time.time;
            float phase = Range(0f, Mathf.PI / 2f);
            for (int i = 0; i < steps; i++)
            {
                float fraction = i / (steps - 1f);
                while (Time.time - startTime < fraction * 1.04f) yield return null;
                for (int arm = 0; arm < 4; arm++)
                {
                    float a = phase + fraction * 3.35f + arm * Mathf.PI * .5f;
                    Vector3 direction = new Vector3(Mathf.Cos(a), Mathf.Sin(a));
                    Color ribbon = arm % 2 == 0 ? cue.petals : cue.tips;
                    Emit(direction * 2.0f, 1.12f, .16f, ribbon);
                    Emit(direction * 1.7f, 1.04f, .08f, Color.Lerp(ribbon, Color.white, .52f));
                }
            }
        }

        private IEnumerator SplitStars()
        {
            const int branches = 6;
            const float splitTime = .68f;
            const float speed = 2.14f;
            float phase = Range(0f, Mathf.PI / 3f);
            var spokes = new Vector3[branches];
            for (int i = 0; i < branches; i++)
            {
                float a = phase + i * (2f * Mathf.PI / branches);
                spokes[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a));
                Emit(spokes[i] * speed, splitTime + .04f, .22f, cue.tips);
            }
            yield return new WaitForSeconds(splitTime);

            for (int i = 0; i < branches; i++)
            {
                // The comet and its daughter cluster use the same constant-speed flight.
                Vector3 offset = spokes[i] * (speed * splitTime);
                Emit(Vector3.zero, .19f, .42f, Color.white, offset);
                for (int j = 0; j < 28; j++)
                {
                    float a = j * 2.399963f + phase;
                    float z = 1f - 2f * (j + .5f) / 28;
                    float r = Mathf.Sqrt(1f - z*z);
                    var direction = new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), z);
                    Color glitter = Color.Lerp(cue.petals, cue.tips, Range(.1f, .85f));
                    Emit(direction * Range(.68f, 1.03f), Range(.74f, 1.16f), Range(.085f, .16f), glitter, offset);
                }
            }
        }

        private float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        private void Emit(Vector3 velocity, float life, float diameter, Color color, Vector3 offset = default)
        {
            var spark = new ParticleSystem.EmitParams
            {
                position = transform.position + offset * scale,
                velocity = velocity * scale,
                startLifetime = life,
                startSize = diameter * scale,
                startColor = color
            };
            bloom.Emit(spark, 1);
        }

        private void OnParticleSystemStopped()
        {
            if (!bursting) return;
            bursting = false;
            finished?.Invoke(this);
            finished = null;
            Destroy(gameObject);
        }

        public void Cancel()
        {
            finished = null;
            rise.Disarm();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
