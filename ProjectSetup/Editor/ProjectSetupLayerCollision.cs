// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace ProjectSetup.Editor
{
    [Serializable]
    internal sealed class ProjectSetupLayerCollision
    {
        [SerializeField] private string firstLayer = string.Empty;
        [SerializeField] private string secondLayer = string.Empty;
        [SerializeField] private bool collisionsEnabled = true;

        internal ProjectSetupLayerCollision()
        {
        }

        internal ProjectSetupLayerCollision(string firstLayer, string secondLayer, bool collisionsEnabled)
        {
            FirstLayer = firstLayer;
            SecondLayer = secondLayer;
            CollisionsEnabled = collisionsEnabled;
        }

        internal string FirstLayer
        {
            get => firstLayer ?? string.Empty;
            set => firstLayer = value ?? string.Empty;
        }

        internal string SecondLayer
        {
            get => secondLayer ?? string.Empty;
            set => secondLayer = value ?? string.Empty;
        }

        internal bool CollisionsEnabled
        {
            get => collisionsEnabled;
            set => collisionsEnabled = value;
        }

        internal ProjectSetupLayerCollision Clone()
        {
            return new ProjectSetupLayerCollision(FirstLayer, SecondLayer, CollisionsEnabled);
        }
    }
}
