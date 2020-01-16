using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Cinemachine;
using System.Linq;

namespace Aircraft {

    public class AircraftArea : MonoBehaviour {

        [Tooltip("The path the race will take")]
        public CinemachineSmoothPath racePath;
        [Tooltip("The prefab to use for checkpoints")]
        public GameObject checkpointPrefab;
        [Tooltip("The prefab to use for the start/end checkpoint")]
        public GameObject finishCheckpointPrefab;
        [Tooltip("If true, enable training mode.")]
        public bool trainingMode;

        public List<AircraftAgent> AircraftAgents { get; private set; }

        public List<GameObject> Checkpoints { get; private set; }

        public AircraftAcademy AircraftAcademy { get; private set; }

        /// <summary>
        /// Actions preformed on script wake.
        /// </summary>
        private void Awake() {
            // GEt all aircraft agents in area. 
            AircraftAgents = transform.GetComponentsInChildren<AircraftAgent>().ToList();
            Debug.Assert(AircraftAgents.Count > 0, "No AircraftAgents found");

            AircraftAcademy = FindObjectOfType<AircraftAcademy>();

        }

        /// <summary>
        /// Set area up.
        /// </summary>
        private void Start() {

            // Create checkpoints along race path.
            Debug.Assert(racePath != null, "Race path not set");
            Checkpoints = new List<GameObject>();

            int numCheckpoints = (int) racePath.MaxUnit(CinemachinePathBase.PositionUnits.PathUnits);

            for(int i=0; i<numCheckpoints; i++) {
                // Create checkpoints and finish-line checkpoint.
                GameObject checkpoint;
                if( i== numCheckpoints) {
                    checkpoint = Instantiate<GameObject>(finishCheckpointPrefab);
                } else {
                    checkpoint = Instantiate<GameObject>(checkpointPrefab);
                }

                // Set checkpoint's parent, position, and rotation.
                checkpoint.transform.SetParent(racePath.transform);
                checkpoint.transform.localPosition = racePath.m_Waypoints[i].position;
                checkpoint.transform.rotation = racePath.EvaluateOrientationAtUnit(i, CinemachinePathBase.PositionUnits.PathUnits);

                // Add checkpoint to the list.
                Checkpoints.Add(checkpoint);
            }
        }

        /// <summary>
        /// Resets agent's position using current NextCheckpointIndex or a random checkpoint.
        /// </summary>
        /// <param name="agent">Agent to be reset</param>
        /// <param name="randomize">If true, pick random next checkpoint</param>
        public void ResetAgentPosition(AircraftAgent agent, bool randomize = false) {
            if(randomize) {
                agent.NextCheckpointIndex = Random.Range(0, Checkpoints.Count);
            }

            // Set start position to previous checkpoint.
            int previousCheckpointIndex = (agent.NextCheckpointIndex - 1) % Checkpoints.Count;

            float startPosition = racePath.FromPathNativeUnits(previousCheckpointIndex, CinemachinePathBase.PositionUnits.PathUnits);

            // Convert the position on the race path to a point in 3d space.
            Vector3 basePosition = racePath.EvaluatePosition(startPosition);

            // Get oritentation at this position on race path.
            Quaternion orientation = racePath.EvaluateOrientation(startPosition);

            // Calculate horizontal offest (ensure spread out agents)
            Vector3 positionOffset = Vector3.right * (AircraftAgents.IndexOf(agent) - AircraftAgents.Count / 2f) * 10f;

            // Set aircraft position and rotation.
            agent.transform.position = basePosition + orientation * positionOffset;
            agent.transform.rotation = orientation;
        }


    }
}