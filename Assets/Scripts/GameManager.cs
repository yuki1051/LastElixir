﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;
using System;

public class GameManager : MonoBehaviour
{
    [SerializeField]
    private GameObject[] block;
    [SerializeField]
    private GameObject jamBlock;
    /*[SerializeField]
    private Background background = null;*/
    [SerializeField]
    private TrackingCamera trackingcamera = null;//BackGroundの代わりにTrackingCameraにmovingBlock代入
    [SerializeField]
    private Image nextZombieImage = null;
    [SerializeField]
    private Image nextZombieImage2 = null;

    [SerializeField]
    private GameObject SubCamera;
    //サブカメラ

    [SerializeField]
    private GameObject ResultBoard = null;

    [SerializeField]
    private GameObject[] eventAlerts;

    [SerializeField]
    private SoundManager soundManager = null;

    private CameraManager cameMane;
    //カメラチェンジ用のスクリプト

    public static List<GameObject> blList = new List<GameObject>();
    //落ちたブロックの参照保存先
    private GameObject blocks;
    private GameObject movingBlock;
    private bool zombieMoving = false;
    private List<int> nextZombie;
    private int nextZombieNum = -1;

    // Flick
    private Vector3 startTouchPos = Vector3.zero;
    private Vector3 endTouchPos = Vector3.zero;

    // ゾンビを積んだ回数
    private int piledZombies = 0;
    // 次のイベント発生タイミング
    private int nextEventTime = -1;
    private int eventcount = 0;
    private int eventType = 0;

    // ゲームオーバー判定
    private Rect judgeArea = new Rect(0, 0, 0, 0);
    private GameObject judgeTarget = null;
    private bool IsGameOver = false;
    private bool IsFeverTime = false;

    private float maxHeight = 0f;

    void Start()
    {
        blocks = new GameObject("Blocks");

        nextZombie = GenerateZombieList();
        nextZombieNum = nextZombie[0];
        nextZombie.RemoveAt(0);
        piledZombies = 0;
        nextEventTime = GenerateNextEventTime(piledZombies);
        eventcount = 0;
        eventType = 0;
        IsGameOver = false;
        //StartCoroutine(FeverTime());
    }

    private List<int> GenerateZombieList()
    {
        var nextZombieList = new List<int>();
        for (int i = 0; i < block.Length; i++)
        {
            nextZombieList.Add(i);
        }

        //シャッフルする
        return nextZombieList.OrderBy(i => Guid.NewGuid()).ToList();
    }

    private void UpdateNextZombieImage()
    {
        if (nextZombieNum == -1 || zombieMoving)
        {
            nextZombieImage.gameObject.SetActive(false);
            //nextZombieImage2.gameObject.SetActive(false);
        }
        else
        {
            nextZombieImage.gameObject.SetActive(true);
            if (nextZombieNum != -2)
            {
                nextZombieImage.sprite = block[nextZombieNum].GetComponent<SpriteRenderer>().sprite;
            }
            else
            {
                nextZombieImage.sprite = jamBlock.GetComponent<SpriteRenderer>().sprite;
            }
            nextZombieImage2.gameObject.SetActive(true);
            var num = nextZombie[0];
            if (num != -2)
            {
                nextZombieImage2.sprite = block[num].GetComponent<SpriteRenderer>().sprite;
            }
            else
            {
                nextZombieImage2.sprite = jamBlock.GetComponent<SpriteRenderer>().sprite;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (IsGameOver)
            return;

        CheckFlick();
        UpdateNextZombieImage();
        MoveBlock();

        if (Input.GetKeyDown(KeyCode.C))
        {
            Gameover();//C押されたらカメラチェンジ
        }
        CheckGameOver();
    }

    private void CheckGameOver()
    {        
        if (judgeArea.width == 0)
            return;

        if (!judgeArea.Contains(judgeTarget.transform.position))
        {
            var obj = GetHighestObject();
            maxHeight = obj.transform.position.y;
            FailureAction();
            StartCoroutine(ShowResult());
        }
    }

    private bool CheckTapMoon()
    {
        var position = MultiTouch.GetTouchPosition();
        var aTapPoint = Camera.main.ScreenToWorldPoint(position);
        var aCollider2d = Physics2D.OverlapPoint(aTapPoint);

        if (aCollider2d)
        {
            GameObject obj = aCollider2d.transform.gameObject;
            return obj.name == "Moon";
        }

        return false;
    }

    private void MoveBlock()
    {
        if (!zombieMoving)
        {
            return;
        }
        else if (MultiTouch.GetTouch() == TouchInfo.Moved)
        {
            Vector2 position = MultiTouch.GetTouchWorldPosition(Camera.main);
            movingBlock.transform.position = position;
        }
        else if (MultiTouch.GetTouch() == TouchInfo.Ended)
        {
            //Time.timeScale = 1.0F;
            if (CheckTapMoon())
            {
                nextZombieImage.transform.rotation = movingBlock.transform.rotation;
                Destroy(movingBlock);
                movingBlock = null;
                zombieMoving = false;
                return;
            }
            movingBlock.GetComponent<Rigidbody2D>().Resume(movingBlock.gameObject);
            movingBlock.GetComponent<SpriteRenderer>().color = new Color(255, 255, 255, 1f);
            foreach (var collider in movingBlock.GetComponents<BoxCollider2D>())
            {
                collider.enabled = true;
            }
            blList.Add(movingBlock);//落ちたブロックを順々に保存
            trackingcamera.TrackObject = movingBlock;
            movingBlock = null;
            zombieMoving = false;
            nextZombieImage.transform.LookAt(nextZombieImage.transform.position + Vector3.forward);
            nextZombieNum = nextZombie[0];
            nextZombie.RemoveAt(0);
            if (nextZombie.Count() <= 1)
            {
                var list = GenerateZombieList();
                nextZombie.AddRange(list);
            }

            // イベント発生の確認
            if (!IsFeverTime) piledZombies++;
            if (piledZombies == nextEventTime)
            {
                Debug.Log("EVENT!!");
                //eventType = (eventType % 3 == 2) ? 0 : UnityEngine.Random.Range(1, 3);
                eventType = (eventcount % 3 == 2) ? 0 : 1;
                eventcount++;
                nextEventTime = GenerateNextEventTime(piledZombies);

                switch (eventType)
                {
                    case 0:
                        // フィーバータイム
                        StartCoroutine(FeverTime());
                        break;
                    case 1:
                        // お邪魔ゾンビ出現
                        StartCoroutine(AppearJamZombie());
                        break;
                }
            }
        }
    }

    IEnumerator FeverTime()
    {
        var alert = Utility.InstantiateGetComponent<EventAlert>(Camera.main.gameObject, eventAlerts[0]);
        Freeze();
        IsFeverTime = true;
        soundManager.StartFeverTime();

        yield return new WaitForSeconds(10f);

        alert.Ended();
        IsFeverTime = false;
        soundManager.FinishFeverTime();
    }

    IEnumerator AppearJamZombie()
    {
        var alert = Utility.InstantiateGetComponent<EventAlert>(Camera.main.gameObject, eventAlerts[1]);
        nextZombie.Insert(0, -2);

        yield return new WaitForSeconds(2f);

        alert.Ended();
    }

    private int GenerateNextEventTime(int piledZombie)
    {
        // 10 16
        return piledZombie + UnityEngine.Random.Range(3, 6);
    }

    public void Freeze()
    {
        foreach (var block in blList)
        {
            block.GetComponent<Rigidbody2D>().Pause(block.gameObject);
        }
    }

    public void GenerateZombie()
    {
        if (IsGameOver)
            return;

        Vector3 position = MultiTouch.GetTouchWorldPosition(Camera.main);
        GameObject prefab;
        if (nextZombieNum == -2)
            prefab = jamBlock;
        else
            prefab = block[nextZombieNum];
        var obj = Utility.Instantiate(blocks, prefab);
        obj.GetComponent<Rigidbody2D>().Pause(obj.gameObject);
        obj.transform.position = position;
        Debug.Log("ZombiePosition" + obj.transform.position);
        obj.transform.rotation = nextZombieImage.transform.rotation;
        obj.GetComponent<SpriteRenderer>().color = new Color(255, 255, 255, 0.5f);
        foreach (var collider in obj.GetComponents<BoxCollider2D>())
        {
            collider.enabled = false;
        }
        movingBlock = obj;
        zombieMoving = true;
 
        obj.GetComponent<GetTopPosition>().CollisionAction = GetTop;
    }

    void GetTop(GameObject obj)//Actionでひも付けて呼び出されている
    {
        Debug.Log("Cpllision");
        GameObject block = GetLastList(); //最後尾のブロック要素をオブジェクトに保存
        Vector3 position = block.transform.position;//そのブロックの座標を取ってくる
        Debug.Log(position.y);
        //var obj = GetHighestObject();
        //Debug.Log("HIGH:" + obj.transform.position);

        float range = 1f;
        judgeArea = new Rect(position.x - range / 2, position.y - range / 2, range, range);
        Debug.Log(judgeArea);
        judgeTarget = obj;
        if (IsFeverTime)
            obj.GetComponent<Rigidbody2D>().Pause(block.gameObject);
        //obj.GetComponent<GetTopPosition>().enabled = false;
    }

    public GameObject GetHighestObject()
    {
        GameObject highest = null;
        foreach (var obj in blList)
        {
            if (highest == null)
            {
                highest = obj;
            }
            else
            {
                if (obj.transform.position.y > highest.transform.position.y)
                {
                    highest = obj;
                }
            }
        }

        return highest;
    }

    public GameObject GetLastList()//Listの最後尾の要素を渡す
    {
        GameObject obj = blList[blList.Count - 1];
        return obj;
    }

    public GameObject GetAndRemoveList()
    {
        GameObject obj = blList[blList.Count - 1];//Listの最後尾の要素を渡す
        blList.RemoveAt(blList.Count - 1);//そして消す、次回呼ばれたときに最後尾から順々に渡すために
        return obj;
    }

    public Transform GetMiddleList()
    {
        GameObject obj = blList[(int)(blList.Count / 2)];//Listの中間の要素を渡す
        Debug.Log("MiddleOriginalPosi" + obj.transform.position.y);
        return obj.transform;//中間要素の位置情報渡す
    }

    private void CheckFlick()
    {
        if (MultiTouch.GetTouch() == TouchInfo.Began)
        {
            startTouchPos = MultiTouch.GetTouchPosition();
        }
        else if (MultiTouch.GetTouch() == TouchInfo.Ended)
        {
            endTouchPos = MultiTouch.GetTouchPosition();
            var direction = endTouchPos - startTouchPos;

            var aTapPoint = Camera.main.ScreenToWorldPoint(startTouchPos);
            var aCollider2d = Physics2D.OverlapPoint(aTapPoint);

            if (direction.x < 0)
            {
                Debug.Log("LEFT");
                nextZombieImage.transform.Rotate(new Vector3(0, 0, 90f));
            }
            else if (direction.x > 0)
            {
                Debug.Log("RIGHT");
                nextZombieImage.transform.Rotate(new Vector3(0, 0, -90f));
            }
        }
    }

    public int ReturnListSize()//Listの要素数を返す
    {
        return blList.Count;
    }

    public void Gameover()
    {//カメラのチェンジ
        SubCamera.SetActiveRecursively(true);
        cameMane = SubCamera.GetComponent<CameraManager>();
        cameMane.CameraZoomOut();
    }

    private void FailureAction()
    {
        IsGameOver = true;
        int ListSize = ReturnListSize(); //要素数を保存
        GameObject block;
        soundManager.currentBgm.Stop();

        for (int i = ListSize - 1; i >= 0; i--)
        {
            block = GetAndRemoveList(); //最後尾のブロック要素をオブジェクトに保存
            block.GetComponent<Rigidbody2D>().Resume(block.gameObject);
            block.GetComponent<Rigidbody2D>().velocity = new Vector3(1.0f * UnityEngine.Random.Range(-5.0f, 5.0f), 10.0f * UnityEngine.Random.value, 0);
        }
    }

    IEnumerator ShowResult()
    {
        yield return new WaitForSeconds(3);
        soundManager.StartResult();
        var resultBoard = Utility.InstantiateGetComponent<ResultBoard>(null, ResultBoard);
        var height = maxHeight / 1.25f;
        int rank = (IsGameOver) ? -1 : (int)(height / 5f);
        float next = 5f - (height % 5f);
        resultBoard.SetScore((int)height, rank, next);
    }
}
