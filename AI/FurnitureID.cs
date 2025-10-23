using UnityEngine;

namespace JY
{
    public class FurnitureID : MonoBehaviour
    {
        [SerializeField] private int id;
        [SerializeField] private ObjectsDatabaseSO database;

        public int ID => id;
        
        public ObjectData Data => database.objectsData.Find(x => x.ID == id);

        private void Awake()
        {
            if (database == null)
            {
            }
        }
    }
} 