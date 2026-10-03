using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Ricochet.Core
{
    /// <summary>
    /// Answers "is this screen position on top of a UI element?".
    /// Used so that pressing a button never also places the beacon or fires.
    /// </summary>
    public static class UIHitTest
    {
        static readonly List<RaycastResult> s_Results = new List<RaycastResult>();
        static PointerEventData s_Pointer;
        static EventSystem s_PointerOwner;

        public static bool IsOverUI(Vector2 screenPosition)
        {
            var es = EventSystem.current;
            if (es == null) return false;

            if (s_Pointer == null || s_PointerOwner != es)
            {
                s_Pointer = new PointerEventData(es);
                s_PointerOwner = es;
            }

            s_Pointer.position = screenPosition;
            s_Results.Clear();
            es.RaycastAll(s_Pointer, s_Results);
            return s_Results.Count > 0;
        }
    }
}
