using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using MLAgents;
using System;

namespace Aircraft {

    public class AircraftAgent : Agent {

        [Header("Movement Parameters")]
        public float thrust = 100000f;
        public float pitchSpeed = 100f;
        public float yawSpeed = 100f;
        public float rollSpeed = 100f;
        public float boostMultiplier = 2f;

        [Header("Explosion stuff")]
        [Tooltip("The aircraft mesh that will disappear on explosion.")]
        public GameObject meshObject;

        [Tooltip("The game object of the explosion particle effect")]
        public GameObject explosionEffect;

        [Header("Training")]
        [Tooltip("Number of steps to timeout after in training if checkpoint is not reached.")]
        public int stepTimeout = 300;

        public int NextCheckpointIndex { get; set; }

        // Components to keep track off
        private AircraftArea area;
        new private Rigidbody rigidbody;
        private TrailRenderer trail;
        private RayPerception3D rayPerception;

        // When next step timeout will be during training.
        private float nextStepTimout;

        // Whether aircraft is frozen (not flying.)
        private bool frozen = false;

        // Controls
        private float pitchChange = 0f;
        private float smoothPitchChange = 0f;   // Smooth turn
        private float maxPitchAngle = 45f;      // Max angle to turn by. 

        private float yawChange = 0;
        private float smoothYawChange = 0f; // No limit on yaw change.

        private float rollChange = 0f;
        private float smoothRollChange = 0f;    
        private float maxRollAngle = 45f;
        private bool boost;

        public override void InitializeAgent() {
            base.InitializeAgent();

            area = GetComponentInParent<AircraftArea>();
            rigidbody = GetComponent<Rigidbody>();
            trail = GetComponent<TrailRenderer>();
            rayPerception = GetComponent<RayPerception3D>();

            // Set step dependent on training or not (5000 or inf if racing.)
            agentParameters.maxStep = area.trainingMode ? 5000 : 0;
        }

        /// <summary>
        /// Read action inputs from vector action.
        /// </summary>
        /// <param name="vectorAction">Chosen action</param>
        /// <param name="textAction">Chosen actiopn text (unused)</param>
        public override void AgentAction(float[] vectorAction, string textAction) {

            // Read values for pitch and yaw
            pitchChange = vectorAction[0]; //(either 0 no change,1 pitch up, 2 pitch down)
            if (pitchChange == 2) pitchChange = -1f;

            yawChange = vectorAction[1];
            if (yawChange == 2) yawChange = -1f;

            // Boost value
            boost = vectorAction[2] == 1; //(0 no boost, 1 boost)
            if (boost && !trail.emitting) {
                trail.Clear();
            }
            trail.emitting = boost;

            if (frozen) return;

            ProcessMovement();

            // Assign reward for action.
            if (area.trainingMode) {
                // Small negative reward every step.
                AddReward(-1f / agentParameters.maxStep);

                // Check if training timeout reached.
                if (GetStepCount() > nextStepTimout) {
                    // negative reward for timing out.
                    AddReward(-.5f);
                    Done();
                }

                // Set position to next checkpoint.
                Vector3 localCheckpointDirection = VectorToNextCheckpoint();

                // L73
                if (localCheckpointDirection.magnitude < area.AircraftAcademy.resetParameters["checkpoint_radius"]) {
                    GotCheckpoint();
                }

            }
        }

        /// <summary>
        /// Prevent the agent from moving/taking actions.
        /// </summary>
        public void FreezeAgent() {
            Debug.Assert(area.trainingMode == false, "Freeze/Thaw not supported in training.");
            frozen = true;
            rigidbody.Sleep();
            trail.emitting = false;
        }

        /// <summary>
        /// Resume agent movements/actions.
        /// </summary>
        public void ThawAgent() {
            Debug.Assert(area.trainingMode == false, "Freeze/Thaw not supported in training.");
            frozen = false;
            rigidbody.WakeUp();
        }

        /// <summary>
        /// Collects observations used by agent to make decisions.
        /// </summary>
        public override void CollectObservations() {
            //Note: obs are local wrt to agent. 
            // Observe aircraft velocity (1 vector3 -> 3 values)
            AddVectorObs(transform.InverseTransformDirection(rigidbody.velocity));

            // Location of next checkpoint (1 vector3 -> 3 values)
            AddVectorObs(VectorToNextCheckpoint());

            // Orientation of the next checkpoint (1 vector3 -> 3 values)
            Vector3 nextCheckpointForward = area.Checkpoints[NextCheckpointIndex].transform.forward;
            AddVectorObs(transform.InverseTransformDirection(nextCheckpointForward));

            // See L79 for ray diagram.
            // Observe ray perception results. 
            string[] detectableObjects = {"Untagged", "checkpoint"};

            // Look ahead and upward
            // (2 tags + 1hit/not + 1 distance to object) * 3 ray angles -> 12 values.
            AddVectorObs(rayPerception.Perceive(
                rayDistance: 250f,
                rayAngles: new float[] { 60f, 90f, 120f },
                detectableObjects: detectableObjects,
                startOffset: 0f, // y axis offset
                endOffset: 75f
                ));

            // Look at several angles along horizon.
            // (2 tags + 1hit/not + 1 distance to object) * 7 ray angles -> 28 values.
            AddVectorObs(rayPerception.Perceive(
                rayDistance: 250f,
                rayAngles: new float[] { 60f, 70f, 80f, 90f, 100f, 110f, 120f },
                detectableObjects: detectableObjects,
                startOffset: 0f, // y axis offset
                endOffset: 75f
                ));

            // Look ahead and downward
            // (2 tags + 1hit/not + 1 distance to object) * 3 ray angles -> 12 values.
            AddVectorObs(rayPerception.Perceive(
                rayDistance: 250f,
                rayAngles: new float[] { 60f, 90f, 120f },
                detectableObjects: detectableObjects,
                startOffset: 0f, // y axis offset
                endOffset: 75f
                ));

            // Total obs = 3+3+3+12+28+12 = 61
        }

        public override void AgentReset() {
            // Reset velocity, position and orientation.
            rigidbody.velocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            trail.emitting = false;
            area.ResetAgentPosition(agent: this, randomize: area.trainingMode);

            // Update step timeout
            if (area.trainingMode) {
                nextStepTimout = GetStepCount() + stepTimeout;
            }
        }

        /// <summary>
        /// Called when agent flys through the correct checkpoint.
        /// </summary>
        private void GotCheckpoint() {
            // Next checkpoint reached, update.
            NextCheckpointIndex = (NextCheckpointIndex + 1) % area.Checkpoints.Count;

            // Reward agent for reaching checkpoint.  
            if (area.trainingMode) {
                AddReward(.5f);
                nextStepTimout = GetStepCount() + stepTimeout;
            }
        }

        /// <summary>
        /// Gets vector to the next checkpoint to fly through.
        /// </summary>
        /// <returns>Local space vector.</returns>
        private Vector3 VectorToNextCheckpoint() {
            // Vector distance between agent and checkpoint
            Vector3 nextCheckpointDirection = area.Checkpoints[NextCheckpointIndex].transform.position - transform.position;
            Vector3 localCheckpointDirection = transform.InverseTransformDirection(nextCheckpointDirection);
            return localCheckpointDirection;
        }

        /// <summary>
        /// Calculate and apply movement.
        /// </summary>
        private void ProcessMovement() {

            // Calculate boost
            float boostModifier = boost ? boostMultiplier : 1f;

            // Apply forward thrust
            rigidbody.AddForce(transform.forward * thrust * boostModifier, ForceMode.Force);

            // Get the current rotation
            Vector3 curRot = transform.rotation.eulerAngles;

            // Calculate roll angle in [-180, 180] degrees.
            float rollAngle = curRot.z > 180 ? curRot.z - 360f : curRot.z;
            if (yawChange == 0) {
                // Not turning, smoothly roll toward center
                rollChange = -rollAngle / maxRollAngle;
            } else {
                // Turning, roll in opposite direction of turn.
                rollChange = -yawChange;
            }

            // Calculate smooth deltas
            smoothPitchChange = Mathf.MoveTowards(smoothPitchChange, pitchChange, 2f * Time.fixedDeltaTime);
            smoothYawChange = Mathf.MoveTowards(smoothYawChange, yawChange, 2f * Time.fixedDeltaTime);
            smoothRollChange = Mathf.MoveTowards(smoothRollChange, rollChange, 2f * Time.fixedDeltaTime);

            // Calculate new pitch, yaw, and roll. Clamp pitch and roll.
            float pitch = ClampAngle(curRot.x + smoothPitchChange * Time.fixedDeltaTime * pitchSpeed,
                                        -maxPitchAngle,
                                        maxPitchAngle);

            float yaw = curRot.y + smoothYawChange * Time.fixedDeltaTime * yawSpeed;

            float roll = ClampAngle(curRot.z + smoothRollChange * Time.fixedDeltaTime * rollSpeed,
                                        -maxRollAngle,
                                        maxRollAngle);

            // Set new rotation
            transform.rotation = Quaternion.Euler(pitch, yaw, roll);

        }

        /// <summary>
        /// Clamp angle between two values
        /// </summary>
        /// <param name="angle">Angle to clamp</param>
        /// <param name="from">Lower limit</param>
        /// <param name="to">Upper limit</param>
        /// <returns></returns>
        private static float ClampAngle(float angle, float from, float to) {
            if (angle < 0f)
                angle = 360 + angle;
            if (angle > 180)
                return Mathf.Max(angle, 360f + from);

            return Mathf.Min(angle, to);
        }

        /// <summary>
        /// React to entering a trigger event.
        /// </summary>
        /// <param name="other">The collider event occured on.</param>
        private void OnTriggerEnter(Collider other) {
            
            // Agent has reached the next checkpoint.
            if (other.transform.CompareTag("checkpoint") && other.gameObject.Equals(area.Checkpoints[NextCheckpointIndex])) {
                GotCheckpoint();
            }
        }

        /// <summary>
        /// React to collision.
        /// </summary>
        /// <param name="collision">Collision info</param>
        private void OnCollisionEnter(Collision collision) {

            // Collision with something that is not an agent.
            if (!collision.transform.CompareTag("agent")) {
                
                if (area.trainingMode) {
                    AddReward(-1f);
                    Done();
                    return;
                } else {
                    // Explosion will take some time to run; start coroutine.
                    StartCoroutine(ExplosionReset());
                }
            }
        }

        /// <summary>
        /// Show explosion,
        /// Resets aircraft to the most recent complete checkpoint.
        /// </summary>
        /// <returns> yield return</returns>
        private IEnumerator ExplosionReset() {
            FreezeAgent();

            meshObject.SetActive(false);
            explosionEffect.SetActive(true);
            yield return new WaitForSeconds(2f);

            // Disable expolision, re-enable aircraft mesh.
            meshObject.SetActive(true);
            explosionEffect.SetActive(false);
            area.ResetAgentPosition(agent: this);
            yield return new WaitForSeconds(1f);

            ThawAgent();

        }
    }

}
