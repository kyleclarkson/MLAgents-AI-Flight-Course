using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aircraft {

    public class Rotate : MonoBehaviour {

        public Vector3 rotateSpeed;

        void Update() {

            // Rotate by this amount in local space. 
            transform.Rotate(rotateSpeed * Time.deltaTime, Space.Self);
        }
    }
}
