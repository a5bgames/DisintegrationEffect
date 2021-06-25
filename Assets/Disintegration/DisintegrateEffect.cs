using System.Collections.Generic;

using Unity.Collections;

using UnityEngine;
using UnityEngine.Rendering;

namespace A5BGames.DisintegrationEffect
{
    public class DisintegrateEffect : MonoBehaviour
    {
        private Material sourceMaterial;                    // material from the source object
        private Mesh particleMesh;                          // particle mesh
        private Material particleMaterial;                  // particle material

        // read-only data lists
        private List<Mesh> triangleMeshes;                  // triangle meshes (form source object)
        private List<Vector3> initialPositions;             // initial positions
        private List<Vector3> particleVelocities;           // particle velocities
        private List<float> particleDelays;                 // particle delays
        private List<float> particleLifetimes;              // particle lifetimes

        // read-write data lists
        private List<float> particleAges;                   // particle ages
        private List<int> activeParticles;                  // the indicies of the particles that are currently active

        // list of TRS matrices for particles being rendered (updated each frame)
        private readonly List<Matrix4x4> renderMatrices = new List<Matrix4x4> ();

        public static DisintegrateEffect Create (Vector3 position, Quaternion rotation) {
            // get the GameObject
            var go = new GameObject ();
            go.transform.SetPositionAndRotation (position, rotation);

            // attach a DisintegrationEffect component
            var effect = go.AddComponent<DisintegrateEffect> ();
            return effect;
        }

        private void Update () {
            // exit if active particle count is zero
            if (activeParticles.Count == 0) {
                return;
            }

            RenderEffect ();
        }

        public void Play (List<Mesh> triangleMeshes, Material sourceMaterial, Mesh particleMesh, Material particleMaterial, List<Vector3> initialPositions, List<Vector3> particleVelocities, List<float> particleLifetimes, List<float> particleDelays, List<float> particleAges, List<int> activeParticles) {
            this.triangleMeshes = triangleMeshes;
            this.sourceMaterial = sourceMaterial;
            this.particleMesh = particleMesh;
            this.particleMaterial = particleMaterial;
            this.initialPositions = initialPositions;
            this.particleVelocities = particleVelocities;
            this.particleLifetimes = particleLifetimes;
            this.particleDelays = particleDelays;
            this.particleAges = particleAges;
            this.activeParticles = activeParticles;

            // render the effect (1st pass)
            // this is necessary, otherwise you'll get a flicker as the source renderer is turned off the frame
            // before the first update is executed on the RenderEffect (depending on object execution order)
            RenderEffect ();
        }

        private void RenderEffect () {
            // store a local reference to the main camera
            var mainCamera = Camera.main;

            // get the camera rotation (particles need to be facing the camera)
            var cameraRotation = mainCamera.transform.rotation;

            // locally store object position and rotation
            var objectPosition = transform.position;
            var objectRotation = transform.rotation;

            // loop over the active particles to render and update their data. loop backward because this
            // list will be deleted from while looping.
            for (int i = activeParticles.Count - 1; i >= 0; i--) {
                // get the particle index for the current active particle
                var particleIndex = activeParticles[i];

                // is the particle still within the delay window?
                var isDelayOver = particleAges[particleIndex] > particleDelays[particleIndex];

                // determine the scale based on the current age (only scale once the delay is over)
                var scale = isDelayOver ? (particleAges[particleIndex] - particleDelays[particleIndex]) / particleLifetimes[particleIndex] : 0;

                // determine the speed step based on whether it is still within the delayed window (if so, the speed is zero)
                var speedStep = isDelayOver ? 1 : 0;

                // calculate the position based on the initial position, velocity, age, and object rotation
                var position = initialPositions[particleIndex] + ((particleAges[particleIndex] - particleDelays[particleIndex]) * speedStep * particleVelocities[particleIndex]);
                position = (objectRotation * position) + objectPosition;

                // when within delay, render the triangle. after the delay, render a particle.
                if (isDelayOver) {
                    var matrix = Matrix4x4.TRS (position, cameraRotation, Vector3.one * (1 - scale));
                    renderMatrices.Add (matrix);
                }
                else {
                    var matrix = Matrix4x4.TRS (position, objectRotation, Vector3.one);
                    Graphics.DrawMesh (triangleMeshes[particleIndex], matrix, sourceMaterial, 0, mainCamera, 0, null, true, true, false);
                }

                // update the age
                particleAges[particleIndex] += Time.deltaTime;

                // delete the particle if it has exceeded its lifetime
                if ((particleAges[particleIndex] - particleDelays[particleIndex]) >= particleLifetimes[particleIndex]) {
                    // swap this active particle to the back of the list and remove
                    activeParticles.RemoveAtSwapBack (i);
                }
            }

            // draw the particles
            if (renderMatrices.Count > 0) {
                DrawParticles (mainCamera);
            }
        }

        private void DrawParticles (Camera camera) {
            // for each TRS matrix, render a particle
            for (int i = 0; i < renderMatrices.Count - 1;) {
                // get the data to draw (DrawMeshInstanced only support drawing 1023 meshes at a time)
                var count = Mathf.Min (1023, renderMatrices.Count - i);
                var matrixRange = renderMatrices.GetRange (i, count);

                // draw instanced
                Graphics.DrawMeshInstanced (particleMesh, 0, particleMaterial, matrixRange, null, ShadowCastingMode.On, true, 0, camera);
                i += count;
            }

            // clear the render matrices for the next frame
            renderMatrices.Clear ();
        }
    }
}