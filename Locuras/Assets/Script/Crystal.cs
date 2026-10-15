using UnityEngine;

public class Crystal : MonoBehaviour, IExtractable
{
    public void Extract()
    {
        if (Flask.Instance != null) Flask.Instance.Add();
    }
}
