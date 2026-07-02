using System;
using UnityEngine;

public class ParallaxEffect : MonoBehaviour {
    private float m_startingPos; //This is the starting position of the sprites.
    private float m_lengthOfSprite; //This is the length of the sprites.
    [SerializeField] private float m_amountOfParallax; //This is amount of parallax scroll. 
    [SerializeField] private Camera m_mainCamera; //Reference of the camera.

    private void Start() {
        m_startingPos = transform.position.x;
        m_lengthOfSprite = GetComponent<SpriteRenderer>().bounds.size.x;
    }

    private void Update() {
        Vector3 position = m_mainCamera.transform.position;
        float temp = position.x * (1 - m_amountOfParallax);
        float distance = position.x * m_amountOfParallax;
        Vector3 newPosition = new Vector3(m_startingPos + distance, transform.position.y, transform.position.z);

        transform.position = newPosition;
        if (temp > m_startingPos + (m_lengthOfSprite / 2)) {
            m_startingPos += m_lengthOfSprite;
        } else if (temp < m_startingPos - (m_lengthOfSprite / 2)) {
            m_startingPos -= m_lengthOfSprite;
        }
    }
}