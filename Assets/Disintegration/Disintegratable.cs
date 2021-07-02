using System.Collections.Generic;

using UnityEngine;

namespace A5BGames.DisintegrationEffect
{
    public class Disintegratable : MonoBehaviour
    {
        private Mesh sourceMesh;
        private Material sourceMaterial;
        private Renderer sourceRenderer;

        [Header ("Particle")]
        [SerializeField]
        private Mesh particleMesh;

        [SerializeField]
        private Material particleMaterial;

        // the speed at which a particle will move away from the origin point
        [SerializeField]
        private float particleSpeed;

        // the speed at which a particle will spread out along its normal
        [SerializeField]
        private float particleSpread;

        // a velocity that is uniform across all particles
        [SerializeField]
        private Vector3 particleDrift;

        [Header ("Lifetime")]
        [SerializeField]
        [Min (0.001f)]
        private float minLifetime;

        [SerializeField]
        [Min (0.001f)]
        private float maxLifetime;

        // the amount a particle is delayed based on its distance from the
        // origin point (in seconds per meter)
        [Header ("Delay")]
        [SerializeField]
        private float delayFactor;

        // the minimum amount of delay (additive with the delay factor)
        [SerializeField]
        [Min (0)]
        private float delayVarianceMinimum;

        // the maximum amount of delay (additive with the delay factor)
        [SerializeField]
        [Min (0)]
        private float delayVarianceMaximum;

        [Header ("Loop")]
        [SerializeField]
        private bool loopEffect;

        [SerializeField]
        [Min (0)]
        private float loopDelay;

        // immutable data
        private List<Mesh> triangleMeshes;                  // meshes
        private List<Vector3> initialPositions;             // initial positions
        private List<Vector3> triangleNormals;              // normals
        private List<float> particleLifetimes;              // lifetime

        private float loopTime = 0f;

        private void Start () {
            // if the source renderer is a SkinnedMeshRenderer, get the source information from
            // that
            var smr = GetComponent<SkinnedMeshRenderer> ();
            if (smr != null) {
                sourceMesh = smr.sharedMesh;
                sourceMaterial = smr.sharedMaterial;
                sourceRenderer = smr;
            } 
            
            // otherwise, the information should be pulled from MeshFilter and MeshRenderer
            // componenets
            else {
                sourceMesh = GetComponent<MeshFilter> ().sharedMesh;
                var mr = GetComponent<MeshRenderer> ();
                sourceMaterial = mr.sharedMaterial;
                sourceRenderer = mr;
            }

            // decompose the source mesh into individual triangle meshes
            DecomposeMesh ();
        }

        private void DecomposeMesh () {
            // store local references to mesh data 
            var meshTriangles = sourceMesh.triangles;
            var meshVertices = sourceMesh.vertices;
            var meshUV = sourceMesh.uv;
            var meshNormals = sourceMesh.normals;

            // allocate the lists
            var triangleCount = meshTriangles.Length;
            triangleMeshes = new List<Mesh> (triangleCount);
            initialPositions = new List<Vector3> (triangleCount);
            triangleNormals = new List<Vector3> (triangleCount);
            particleLifetimes = new List<float> (triangleCount);

            // process every triangle in the source mesh
            for (int t = 0; t < triangleCount;) {
                // get the indices for the current triangle
                var t0 = meshTriangles[t];
                var t1 = meshTriangles[t + 1];
                var t2 = meshTriangles[t + 2];

                // get the vertices
                var vertex0 = meshVertices[t0];
                var vertex1 = meshVertices[t1];
                var vertex2 = meshVertices[t2];

                // calculate the triangle position (in object-space coordinates)
                var trianglePosition = (vertex0 + vertex1 + vertex2) / 3f;

                // create vertex array for triangle mesh. vertex positions
                // should be in object-space coordinates (for the disintegratable
                // object)
                var vertices = new Vector3[3] {
                    vertex0 - trianglePosition,
                    vertex1 - trianglePosition,
                    vertex2 - trianglePosition
                };

                // create uv array for triangle mesh
                var uvs = new Vector2[3] {
                    meshUV[t0],
                    meshUV[t1],
                    meshUV[t2]
                };

                // get the normals
                var normal0 = meshNormals[t0];
                var normal1 = meshNormals[t1];
                var normal2 = meshNormals[t2];


                // create normal array for triangle mesh
                var normals = new Vector3[3] {
                    normal0,
                    normal1,
                    normal2
                };

                // calculate the triangle normal (average of vertex normals)
                var triangleNormal = (normals[0] + normals[1] + normals[2]) / 3;

                // create a new mesh for the current triangle from the source mesh
                var triangleMesh = new Mesh {
                    vertices = vertices,
                    uv = uvs,
                    normals = normals,
                    triangles = new int[] { 0, 1, 2 }
                };

                // calculate the particle lifetime
                var lifetime = Random.Range (minLifetime, maxLifetime);

                // add the triangle/particle data to the lists
                initialPositions.Add (trianglePosition);
                triangleNormals.Add (triangleNormal);
                triangleMeshes.Add (triangleMesh);
                particleLifetimes.Add (lifetime);

                t += 3;
            }
        }

        public void Disintegrate (Vector3 originPoint) {
            // count of triangles/particles
            var count = initialPositions.Count;

            // create lists for generated data
            var particleVelocities = new List<Vector3> (count);                 // the velocities for each particle
            var particleDelay = new List<float> (count);                        // the delay for each particle
            var particleAge = new List<float> (count);                          // the age of each particle
            var activeParticles = new List<int> (count);                        // the indices of the active particles in the data lists

            // translate origin point from worldspace to objectspace
            originPoint -= transform.position;

            // get the object rotation
            var objectRotation = transform.rotation;

            // loop over particles
            for (int i = 0; i < initialPositions.Count; i++) {
                // get particle position and normal, rotated by the object rotation
                var position = objectRotation * initialPositions[i];
                var normal = objectRotation * triangleNormals[i];

                // calculate velocity
                // velocity is the speed moving from the particle away from the origin,
                // and also moving along the normal of the triangle in the source mesh,
                // combined with the uniform drift.
                var velocity = (position - originPoint).normalized * particleSpeed;
                velocity += normal * particleSpread;
                velocity += particleDrift;

                // calculate the delay
                // delay is determined by the distance of the particle from the origin
                // point plus a random value between the variance min and max.
                var delay = Vector3.Distance (position, originPoint) * delayFactor;
                delay += Random.Range (delayVarianceMinimum, delayVarianceMaximum);

                if ((delay + particleLifetimes[i]) > loopTime) {
                    loopTime = delay + particleLifetimes[i];
                }
                
                // add data to the lists
                particleVelocities.Add (velocity);
                particleDelay.Add (delay);
                particleAge.Add (0);

                // add the index of the current particle/triange to the active list
                activeParticles.Add (i);
            }

            // disable the source renderer
            sourceRenderer.enabled = false;

            // Create and Play the effect
            var effect = DisintegrateEffect.Create (transform.position, transform.rotation);
            effect.Play (
                triangleMeshes, sourceMaterial, particleMesh, particleMaterial, 
                initialPositions, particleVelocities, particleLifetimes, particleDelay, 
                particleAge, activeParticles, loopEffect, (loopTime + loopDelay / 2f), () => {
                    // if looping, renable source renderer on completion of the effect
                    if (loopEffect) {
                        sourceRenderer.enabled = true;
                    }
                }
            );
        }
    }
}