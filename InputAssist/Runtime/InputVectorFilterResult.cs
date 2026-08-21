using UnityEngine;

namespace InputAssist
{
    /// <summary>Contains the accepted raw vector, processed vector, direction, and failure reason.</summary>
    public readonly struct InputVectorFilterResult
    {
        /// <summary>Gets whether processing succeeded.</summary>
        public bool Succeeded => Error == InputAssistError.None;

        /// <summary>Gets the rejected or accepted raw input.</summary>
        public Vector2 RawInput { get; }

        /// <summary>Gets the current processed vector. A failed request returns the unchanged previous value.</summary>
        public Vector2 Value { get; }

        /// <summary>Gets the direction classified from <see cref="Value"/>.</summary>
        public InputDirection Direction { get; }

        /// <summary>Gets the explicit failure reason.</summary>
        public InputAssistError Error { get; }

        internal InputVectorFilterResult(Vector2 rawInput, Vector2 value, InputDirection direction, InputAssistError error)
        {
            RawInput = rawInput;
            Value = value;
            Direction = direction;
            Error = error;
        }
    }
}
