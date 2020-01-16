using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aircraft {

    public class AircraftPlayer : AircraftAgent {

        [Header("Input bindings")]
        public InputAction pitchInput;
        public InputAction yawInput;
        public InputAction boostInput;
        public InputAction pauseInput;

        public override void InitializeAgent() {
            base.InitializeAgent();
            pitchInput.Enable();
            yawInput.Enable();
            boostInput.Enable();
            pauseInput.Enable();
        }

        /// <summary>
        /// Reads player input and converts to vectorAction array.
        /// </summary>
        /// <returns>Array of floats for AgentAction to use</returns>
        public override float[] Heuristic() {

            // Values coming in will be val<0==down, val=0==none, val>0==up
            float pitchValue = Mathf.Round(pitchInput.ReadValue<float>());
            float yawValue = Mathf.Round(yawInput.ReadValue<float>());
            float boostValue = Mathf.Round(boostInput.ReadValue<float>());

            // Convert -1 (down) to discrete value 2
            if (pitchValue == -1f) pitchValue = 2f;
            if (yawValue == -1f) yawValue = 2f;

            return new float[] { pitchValue, yawValue, boostValue };
        }

        private void OnDestroy() {
            pitchInput.Disable();
            yawInput.Disable();
            boostInput.Disable();
            pauseInput.Disable();
        }
    }
}
