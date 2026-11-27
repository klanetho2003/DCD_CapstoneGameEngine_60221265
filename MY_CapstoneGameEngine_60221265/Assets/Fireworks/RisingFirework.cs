using UnityEngine;

namespace Capstone.Fireworks
{
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class RisingFirework : MonoBehaviour
    {
        private readonly ParticleSystem.Particle[] head = new ParticleSystem.Particle[1];
        private ParticleSystem lift;
        private FireworkShell shell;
        private Vector3 summit;
        private Vector3 lastPosition;
        private float expectedArrival;
        private bool armed;

        public void Ignite(FireworkShell owner, Vector3 start, Vector3 end, float duration, float size)
        {
            shell = owner;
            summit = end;
            lastPosition = start;
            expectedArrival = Time.time + duration;
            transform.position = start;
            lift = GetComponent<ParticleSystem>();
            gameObject.SetActive(true);
            armed = true;
            lift.Play(false);
            var spark = new ParticleSystem.EmitParams
            {
                position = start,
                velocity = (end - start) / duration,
                startLifetime = duration,
                startSize = 0.13f * size,
                startColor = new Color32(255, 222, 158, 255)
            };
            lift.Emit(spark, 1);
        }

        public void Disarm() => armed = false;

        private void LateUpdate()
        {
            if (armed && lift.GetParticles(head) == 1)
                lastPosition = head[0].position;
        }

        private void OnApplicationQuit() => Disarm();

        private void OnDisable()
        {
            if (!armed) return;
            armed = false;
            if (!shell || !shell.isActiveAndEnabled || !gameObject.scene.isLoaded) return;

            // Unity clears the particle buffer as its GameObject deactivates.
            // A manually interrupted ascent uses the last simulated head position.
            Vector3 origin = Time.time >= expectedArrival - Time.deltaTime ? summit : lastPosition;
            shell.BurstAt(origin);
        }
    }
}
