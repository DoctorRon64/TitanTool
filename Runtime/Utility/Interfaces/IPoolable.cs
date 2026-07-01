using UnityEngine;

namespace Utility {
    public interface IPoolable {
        bool active { get; set; }
        void DisablePoolable();
        void EnablePoolable();
        void SetPosition(Vector2 _pos);
    }
}