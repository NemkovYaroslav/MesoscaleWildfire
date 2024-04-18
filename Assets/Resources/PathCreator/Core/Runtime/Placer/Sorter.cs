using System.Linq;
using UnityEngine;

namespace Resources.PathCreator.Core.Runtime.Placer
{
    public class Sorter : MonoBehaviour
    {
        [ContextMenu("Sort Children")]
        public void SortChildren()
        {
            var placers = transform.GetComponentsInChildren<ModulePlacer>();
            var orderedPlacers = placers.OrderBy(property => property.t).ToArray();

            foreach (var placer in orderedPlacers)
            {
                placer.transform.SetAsLastSibling();
            }
        }
    }
}