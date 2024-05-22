using System.Collections.Generic;
using UnityEngine;

namespace QuadTree
{
    public class QuadTreeBridge : MonoBehaviour
    {
        [SerializeField] private QuadTree linkedQuadTree;
        
        public void On2DBoundsCalculated(Rect bounds)
        {
            linkedQuadTree.PrepareTree(bounds);
        }

        public void OnItemSpawned(GameObject itemGo)
        {
            linkedQuadTree.AddData(itemGo.GetComponent<ISpatialData2D>());
        }
        
        public void OnAllItemsSpawned(List<GameObject> items)
        {
            // Intentionally turned off as only one of the add methods should be used

            //List<ISpatialData2D> SpatialItems = new List<ISpatialData2D>(Items.Count);
            //foreach (GameObject Item in Items)
            //{
            //    SpatialItems.Add(Item.GetComponent<ISpatialData2D>());
            //}

            //LinkedQuadTree.AddData(SpatialItems);

            linkedQuadTree.ShowStats();
        }
    }
}
