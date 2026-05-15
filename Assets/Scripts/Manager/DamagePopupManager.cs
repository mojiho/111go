using System.Collections.Generic;
using UnityEngine;

/* 
 * ������ �˾� �Ŵ��� Ŭ���� �Դϴ�.
 * ������ �˾� ������Ʈ�� Ǯ���Ͽ� �����մϴ�.
 */

public class DamagePopupManager : MonoBehaviour
{
    public static DamagePopupManager Instance { get; private set; }

    [SerializeField] private GameObject popupPrefab;
    private Queue<DamagePopup> _pool = new Queue<DamagePopup>();

    private Transform _tr;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        _tr = transform;
    }

    public void ShowPopup(int damage, Vector3 position, Vector3 direction)
    {
        var popup = AcquirePopup(position);
        popup.Setup(damage, direction);
    }

    public void ShowPopup(int damage, Vector3 position, Vector3 direction, Color color)
    {
        var popup = AcquirePopup(position);
        popup.Setup(damage, direction, color);
    }

    private DamagePopup AcquirePopup(Vector3 position)
    {
        DamagePopup popup;
        if (_pool.Count > 0)
            popup = _pool.Dequeue();
        else
        {
            GameObject obj = Instantiate(popupPrefab, _tr);
            popup = obj.GetComponent<DamagePopup>();
        }
        popup.gameObject.SetActive(true);
        popup.transform.position = position;
        return popup;
    }

    public void ReturnPopup(DamagePopup popup)
    {
        popup.gameObject.SetActive(false);
        popup.transform.SetParent(_tr);
        _pool.Enqueue(popup);
    }
}