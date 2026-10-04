using UnityEngine;

public class GameManager : MonoBehaviour
{
    enum player
    {
        player1,
        player2,
        player3,
        player4
    }
    struct PlayerStatus
    {
        private const int maxPass = 5;
        [SerializeField]private int pass;
        private const int firstCoin = 100;
        [SerializeField]private int coin;
    }
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
