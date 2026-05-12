using System.Collections.Generic;
using UnityEngine;

/* 
 * 데미지 팝업 매니저 클래스 입니다.
 * 데미지 팝업 오브젝트를 풀링하여 관리합니다.
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
        DamagePopup popup;

        if (_pool.Count > 0)
        {
            popup = _pool.Dequeue();
        }
        else
        {
            GameObject obj = Instantiate(popupPrefab, _tr);
            popup = obj.GetComponent<DamagePopup>();
        }

        popup.gameObject.SetActive(true);
        popup.transform.position = position;
        popup.Setup(damage, direction);
    }

    public void ReturnPopup(DamagePopup popup)
    {
        popup.gameObject.SetActive(false);
        popup.transform.SetParent(_tr);
        _pool.Enqueue(popup);
    }
}