using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Network.Platformer
{
    public class LifeHandler : MonoBehaviour
    {
        [SerializeField]Image bar;
       
        public void UpdateBar(float progress)
        {
            bar.fillAmount = progress;
        }
    }
}
