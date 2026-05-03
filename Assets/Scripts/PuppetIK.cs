using UnityEngine;

public class PuppetIK : MonoBehaviour
{
    private Animator anim;
    public GameObject targetHand;
    public float weight = 1.0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        anim = GetComponent<Animator>(); 
    }

    private void OnAnimatorIK(int layerIndex)
    {
        anim.SetIKPosition(AvatarIKGoal.LeftHand,targetHand.transform.position);
        anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
