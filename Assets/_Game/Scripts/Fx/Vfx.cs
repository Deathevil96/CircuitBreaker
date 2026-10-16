using UnityEngine;

namespace CircuitBreaker.Fx
{
    public static class Vfx
    {
        private static Material _mat;
        private static Material Mat
        {
            get
            {
                if (_mat == null)
                {
                    _mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = ProcSprites.GlowDot.texture };
                }
                return _mat;
            }
        }

        /// <summary>One-shot particle burst (sparks).</summary>
        public static void Burst(Vector3 pos, Color color, int count = 18, float speed = 4.5f, float size = 0.16f, float life = 0.45f)
        {
            var go = new GameObject("VFX_Burst");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // configure while stopped
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.35f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.05f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0)));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(color, 1) },
                         new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = grad;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mat;
            r.sortingOrder = 40;
            ps.Play();
        }
    }
}
