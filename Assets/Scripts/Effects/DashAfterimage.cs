using System.Collections;
using UnityEngine;

// 대시 잔상 — PlayerController의 TriggerDashEffect에서 호출
// 플레이어 오브젝트에 붙이면 됨
public class DashAfterimage : MonoBehaviour
{
    public GameObject afterimagePrefab;     // SpriteRenderer만 있는 단순 프리팹
    public int imageCount = 4;
    public float intervalTime = 0.03f;
    public Color afterimageColor = new Color(0.5f, 0.8f, 1f, 0.6f);

    private SpriteRenderer sr;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    public void SpawnAfterimage(Vector3 startPos)
    {
        StartCoroutine(SpawnSequence(startPos));
    }

    private IEnumerator SpawnSequence(Vector3 pos)
    {
        for (int i = 0; i < imageCount; i++)
        {
            if (afterimagePrefab != null && sr != null)
            {
                GameObject img = Instantiate(afterimagePrefab, transform.position, transform.rotation);
                SpriteRenderer imgSr = img.GetComponent<SpriteRenderer>();
                if (imgSr != null)
                {
                    imgSr.sprite = sr.sprite;
                    imgSr.flipX = sr.flipX;
                    Color c = afterimageColor;
                    c.a = afterimageColor.a * (1f - (float)i / imageCount);
                    imgSr.color = c;
                }
                Destroy(img, 0.15f);
            }
            yield return new WaitForSecondsRealtime(intervalTime);
        }
    }
}
