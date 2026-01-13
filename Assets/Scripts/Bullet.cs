using UnityEngine;

public class BulletRing : MonoBehaviour
{
    public GameObject bulletPrefab;
    public int bulletCount = 20;
    public float bulletSpeed = 5f;

    public void Fire()
    {
        float angleStep = 360f / bulletCount;
        float angle = 0f;

        for (int i = 0; i < bulletCount; i++)
        {
            float dirX = Mathf.Cos(angle * Mathf.Deg2Rad);
            float dirY = Mathf.Sin(angle * Mathf.Deg2Rad);

            Vector2 direction = new Vector2(dirX, dirY).normalized;

            GameObject bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
            bullet.GetComponent<Rigidbody2D>().linearVelocity = direction * bulletSpeed;

            angle += angleStep;
        }
    }
}