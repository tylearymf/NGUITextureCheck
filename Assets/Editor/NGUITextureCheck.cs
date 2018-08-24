using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;
using Object = UnityEngine.Object;
using System;
using System.Text.RegularExpressions;

public class NGUITextureCheck : EditorWindow
{
    static NGUITextureCheck sInstance;

    [MenuItem("小工具/Texture引用检测")]
    static void ShowWindow()
    {
        sInstance = GetWindow<NGUITextureCheck>();
        sInstance.title = "Texture引用检测";
        sInstance.Show();
    }

    static public void LocationTexture(string pTextureName)
    {
        var tHasWindow = Resources.FindObjectsOfTypeAll<EditorWindow>().Single(x => x.GetType() == typeof(NGUITextureCheck)) != null;
        ShowWindow();
        if (!tHasWindow) sInstance.CheckReference();
        sInstance.mSearchText = pTextureName;
        sInstance.Focus();
    }

    public enum SortType
    {
        Name = 1,
        Count,
        Size,
    }

    public const string cPrefabPath = "Assets/UI/Prefabs";
    public const string cTexturePath = "Assets/UI/CommonTextures";

    static Vector2 sScrollViewPos;

    List<TextureInfo> mTextureInfos;
    SortType mSortType = SortType.Name;
    string mSearchText;
    SortType mLastSortTyp = 0;
    bool mDescending;
    bool mInit;
    bool mMatchFullName;

    /// <summary>
    /// 检测所有Texture引用
    /// </summary>
    void CheckReference()
    {
        var tGuids = AssetDatabase.FindAssets("t:prefab", new string[] { cPrefabPath });
        var tPrefabInfos = new List<PrefabInfo>(tGuids.Length);
        for (int i = 0, imax = tGuids.Length; i < imax; i++)
        {
            var tGuid = tGuids[i];
            var tPath = AssetDatabase.GUIDToAssetPath(tGuid);
            var tAsset = AssetDatabase.LoadAssetAtPath<GameObject>(tPath);
            if (tAsset == null) continue;
            tPrefabInfos.Add(new PrefabInfo(tAsset, tGuid, tPath));
            EditorUtility.DisplayProgressBar(string.Empty, "Prefab数据初始化", i / (float)imax);
        }

        tGuids = AssetDatabase.FindAssets("t:texture", new string[] { cTexturePath });
        mTextureInfos = new List<TextureInfo>(tGuids.Length);
        for (int i = 0, imax = tGuids.Length; i < imax; i++)
        {
            var tGuid = tGuids[i];
            var tPath = AssetDatabase.GUIDToAssetPath(tGuid);
            var tAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(tPath);
            if (tAsset == null) continue;
            mTextureInfos.Add(new TextureInfo(tAsset, tGuid, tPath));
            EditorUtility.DisplayProgressBar(string.Empty, "Texture数据初始化", i / (float)imax);
        }

        if (!tPrefabInfos.IsNullOrEmpty())
        {
            for (int i = 0, imax = mTextureInfos.Count; i < imax; i++)
            {
                var tInfo = mTextureInfos[i];
                var tPrefabs = tPrefabInfos.FindAll(x => x.ExistGuid(tInfo.guid));
                if (tPrefabs.IsNullOrEmpty()) continue;
                tInfo.UpdateReference(tPrefabInfos);
                EditorUtility.DisplayProgressBar(string.Empty, "查找Texture引用", i / (float)imax);
            }
        }
        EditorUtility.ClearProgressBar();
        mInit = true;
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(string.Format("拖拽{0}下的Texture到该窗口下可以快速查看该Texture的引用", cTexturePath), MessageType.Info);
        if (GUILayout.Button("检测所有Texture引用", GUILayout.Height(50)))
        {
            CheckReference();
        }
        GUILayout.Space(10);

        if (mTextureInfos.IsNullOrEmpty())
        {
            if (Event.current.type == EventType.DragUpdated)
            {
                if (!mInit) CheckReference();
            }
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("排序类型", GUILayout.Width(50));
        mSortType = (SortType)EditorGUILayout.EnumPopup(mSortType, GUILayout.Width(50));
        if (GUILayout.Button(mDescending ? "顺序" : "逆序", GUILayout.Width(50)))
        {
            mLastSortTyp = 0;
            mDescending = !mDescending;
        }
        GUI.changed = false;
        mSearchText = EditorGUILayout.TextField(string.Empty, mSearchText, "SearchTextField");
        if (GUI.changed) mMatchFullName = false;
        EditorGUILayout.EndHorizontal();

        if (mSortType != mLastSortTyp)
        {
            mLastSortTyp = mSortType;
            switch (mSortType)
            {
                case SortType.Name:
                    mTextureInfos.Sort((x, y) =>
                    {
                        var xl = x.name.Length;
                        var yl = y.name.Length;
                        if (xl != yl) return xl.CompareTo(yl) * (mDescending ? -1 : 1);

                        var xs = x.name.ToLower().ToCharArray();
                        var ys = y.name.ToLower().ToCharArray();
                        for (int i = 0, imax = Mathf.Min(xs.Length, ys.Length); i < imax; i++)
                        {
                            if (xs[i] != ys[i]) return xs[i].CompareTo(ys[i]) * (mDescending ? -1 : 1);
                        }
                        return x.name.CompareTo(y.name) * (mDescending ? -1 : 1);
                    });
                    break;
                case SortType.Count:
                    mTextureInfos.Sort((x, y) =>
                    {
                        var xl = x.prefabInfos.GetCountIgnoreNull();
                        var yl = y.prefabInfos.GetCountIgnoreNull();
                        return xl.CompareTo(yl) * (mDescending ? -1 : 1);
                    });
                    break;
                case SortType.Size:
                    mTextureInfos.Sort((x, y) =>
                    {
                        var xs = x.size.x * x.size.y;
                        var ys = y.size.x * y.size.y;
                        return xs.CompareTo(ys) * (mDescending ? -1 : 1);
                    });
                    break;
            }
        }

        sScrollViewPos = EditorGUILayout.BeginScrollView(sScrollViewPos);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(10);
        EditorGUILayout.BeginVertical();
        for (int i = 0; i < mTextureInfos.Count; i++)
        {
            var item = mTextureInfos[i];
            if (!IsMatchName(ref mSearchText, item.name, mSearchText)) continue;
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button(item.texture, "ObjectFieldThumb", GUILayout.Width(20), GUILayout.Height(15)))
                    {
                        item.Select();
                    }
                }
                GUILayout.Space(2);
                item.toggle = DrawHeader(item.toggle, item.guiName);
                EditorGUILayout.EndHorizontal();
                if (item.toggle)
                {
                    item.Draw();
                }
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();

        if (Event.current.type == EventType.DragUpdated)
        {
            mMatchFullName = true;
            var tObjects = DragAndDrop.objectReferences;
            if (tObjects != null && tObjects.Length > 0)
            {
                var tTextureNames = new string[tObjects.Length];
                for (int i = 0; i < tObjects.Length; i++)
                {
                    tTextureNames[i] = tObjects[i].name;
                }
                mSearchText = string.Join("|", tTextureNames);
            }
        }
    }

    bool IsMatchName(ref string pSearchText, string pName1, string pName2)
    {
        if (pName2.IsNullOrEmpty()) return true;
        try
        {
            if (pName2.IndexOf('|') != -1)
            {
                var tSplitNames = Regex.Split(pName2, @"\|+");
                for (int i = 0, imax = tSplitNames.Length; i < imax; i++)
                {
                    if (mMatchFullName)
                    {
                        if (pName1 == tSplitNames[i].Trim()) return true;
                    }
                    else
                    {
                        if (pName1.IndexOf(tSplitNames[i].Trim(), StringComparison.OrdinalIgnoreCase) != -1) return true;
                    }
                }
                return false;
            }
            else
            {
                var tSplitNames = Regex.Split(pName2, @"\s+");
                for (int i = 0, imax = tSplitNames.Length; i < imax; i++)
                {
                    if (mMatchFullName)
                    {
                        if (pName1 != tSplitNames[i].Trim()) return false;
                    }
                    else
                    {
                        if (pName1.IndexOf(tSplitNames[i].Trim(), StringComparison.OrdinalIgnoreCase) == -1) return false;
                    }
                }
                return true;
            }
        }
        catch
        {
            Debug.LogError("输入有误");
            pSearchText = string.Empty;
            GUI.FocusControl(string.Empty);
            return false;
        }
    }

    bool DrawHeader(bool pState, string pText)
    {
        if (!pState) GUI.backgroundColor = new Color(0.8F, 0.8F, 0.8F);
        EditorGUILayout.BeginHorizontal();
        {
            GUI.changed = false;
            if (pState) pText = "\u25BC" + (char)0x200a + pText;
            else pText = "\u25BA" + (char)0x200a + pText;

            GUILayout.BeginHorizontal();
            {
                if (!GUILayout.Toggle(true, pText, "Label", GUILayout.MinWidth(20))) pState = !pState;
            }
            GUILayout.EndHorizontal();
        }
        EditorGUILayout.EndHorizontal();
        GUI.backgroundColor = Color.white;
        return pState;
    }

    [Serializable]
    public class PrefabInfo : BaseInfo
    {
        public PrefabInfo(GameObject pGo, string pGuid, string pAssetPath) : base(pGuid, pAssetPath)
        {

            gameObject = pGo;
            mContent = File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
        }

        string mContent;

        public GameObject gameObject { private set; get; }
        public Transform transform
        {
            get
            {
                return gameObject == null ? null : gameObject.transform;
            }
        }

        public override string name
        {
            get
            {
                return gameObject == null ? base.name : gameObject.name;
            }

            protected set
            {
                base.name = value;
            }
        }

        public string[] optionNames { set; get; }
        public int selectOptionIndex { set; get; }
        /// <summary>
        /// 判断是否有引用到该GUID
        /// </summary>
        /// <param name="pGuid"></param>
        /// <returns></returns>
        public bool ExistGuid(string pGuid)
        {
            return mContent.IsNullOrEmpty() || pGuid.IsNullOrEmpty() ? false : mContent.IndexOf(pGuid) != -1;
        }

        public void Select()
        {
            if (!gameObject) return;
            Selection.activeObject = gameObject;
        }

        public void Draw(TextureInfo pInfo)
        {
            if (pInfo == null) return;
            var tPaths = pInfo.referenceDic.ContainsKey(this) ? pInfo.referenceDic[this] : null;

            EditorGUILayout.BeginHorizontal("AS TextureArea", GUILayout.MinHeight(20));
            var tReferenceCount = tPaths.GetCountIgnoreNull();
            EditorGUILayout.LabelField(name + " Count:" + tReferenceCount);
            if (gameObject != null && GUILayout.Button("定位Prefab", GUILayout.Width(80)))
            {
                Select();
            }
            if (tReferenceCount > 0)
            {
                GUI.changed = false;
                if (tReferenceCount > 1)
                {
                    selectOptionIndex = EditorGUILayout.Popup(selectOptionIndex, optionNames, GUILayout.Width(90));
                }
                if (GUI.changed || (tReferenceCount == 1 && GUILayout.Button("定位Texture", GUILayout.Width(90))))
                {
                    if (selectOptionIndex < 0 || selectOptionIndex >= tReferenceCount) selectOptionIndex = 0;
                    var tPath = tReferenceCount == 0 ? string.Empty : tPaths[selectOptionIndex];

                    var tGo = GameObject.Find(name);
                    if (tGo == null)
                    {
                        var tRoot = GameObject.Find("UIRoot");
                        if (tRoot != null)
                        {
                            tGo = PrefabUtility.InstantiatePrefab(gameObject) as GameObject;
                            if (tGo != null)
                            {
                                tGo.transform.SetParent(tRoot.transform);
                                tGo.transform.localPosition = Vector3.zero;
                                tGo.transform.localRotation = Quaternion.identity;
                                tGo.transform.localScale = Vector3.one;
                            }
                        }
                    }

                    if (tGo != null)
                    {
                        tGo.name = name;

                        var tFind = TextureInfo.GetTrasnformByPath(tGo, pInfo.name, tPath, selectOptionIndex);
                        if (tFind != null) Selection.activeTransform = tFind;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    [Serializable]
    public class TextureInfo : BaseInfo
    {
        public TextureInfo(Texture2D pTexture, string pGuid, string pAssetPath) : base(pGuid, pAssetPath)
        {
            texture = pTexture;
            size = texture == null ? Vector2.zero : new Vector2(texture.width, texture.height);
        }

        bool mToggle;
        Dictionary<PrefabInfo, List<string>> mReferenceDic;

        public Texture2D texture { private set; get; }
        public override string name
        {
            get
            {
                return texture == null ? base.name : texture.name;
            }
            protected set
            {
                base.name = value;
            }
        }

        public bool toggle
        {
            get { return mToggle; }
            set { mToggle = value; }
        }

        public string guiName
        {
            get
            {
                return string.Format("{0} Count:{1} Size:{2}*{3}", name, prefabInfos.GetCountIgnoreNull(), (int)size.x, (int)size.y);
            }
        }

        public Vector2 size { private set; get; }

        /// <summary>
        /// 以下prefab里面引用这该texture
        /// </summary>
        public List<PrefabInfo> prefabInfos { private set; get; }

        List<SerializaTextureDictionary> serializaReference { set; get; }

        /// <summary>
        /// key为prefab，value为texture路径
        /// </summary>
        public Dictionary<PrefabInfo, List<string>> referenceDic
        {
            private set
            {
                mReferenceDic = value;
            }
            get
            {
                //代码编译后，重新初始化数据
                if (mReferenceDic == null && !serializaReference.IsNullOrEmpty())
                {
                    mReferenceDic = new Dictionary<PrefabInfo, List<string>>(serializaReference.Count);
                    foreach (var item in serializaReference)
                    {
                        if (mReferenceDic.ContainsKey(item.key)) continue;
                        mReferenceDic.Add(item.key, item.value);
                    }
                    prefabInfos = new List<PrefabInfo>(mReferenceDic.Keys);
                    CalculateOptionName();
                }
                return mReferenceDic;
            }
        }

        public void UpdateReference(List<PrefabInfo> pPrefabInfos)
        {
            if (name.IsNullOrEmpty()) return;
            prefabInfos = pPrefabInfos;
            CalculateReference();
        }

        void CalculateReference()
        {
            if (prefabInfos.IsNullOrEmpty()) return;
            referenceDic = new Dictionary<PrefabInfo, List<string>>();
            serializaReference = new List<SerializaTextureDictionary>();
            foreach (var tPrefabInfo in prefabInfos)
            {
                if (tPrefabInfo == null || !tPrefabInfo.gameObject) continue;
                var tRoot = tPrefabInfo.transform;
                var tTexture = tRoot.GetComponent<UITexture>();
                if (CheckTextureIsEqual(tRoot, tTexture, name, string.Empty))
                {
                    AddPathToDic(tPrefabInfo, string.Empty);
                }

                var tPanel = tRoot.GetComponent<UIPanel>();
                if (CheckPanelIsEqual(tPanel, name))
                {
                    AddPathToDic(tPrefabInfo, string.Empty);
                }

                var tChildTextures = tRoot.GetComponentsInChildren<UITexture>(true);
                foreach (var tChildTexture in tChildTextures)
                {
                    if (!CheckTextureIsEqual(tRoot, tChildTexture, name, string.Empty)) continue;
                    AddPathToDic(tPrefabInfo, tChildTexture.transform.GetHierarchyByRoot(tRoot));
                }

                var tChildPanels = tRoot.GetComponentsInChildren<UIPanel>(true);
                foreach (var tChildPanel in tChildPanels)
                {
                    if (!CheckPanelIsEqual(tChildPanel, name)) continue;
                    AddPathToDic(tPrefabInfo, tChildPanel.transform.GetHierarchyByRoot(tRoot));
                }
            }
            CalculateOptionName();
        }

        void AddPathToDic(PrefabInfo pInfo, string pPath)
        {
            if (!referenceDic.ContainsKey(pInfo))
            {
                referenceDic.Add(pInfo, new List<string>());
                serializaReference.Add(new SerializaTextureDictionary(pInfo, new List<string>()));
            }
            referenceDic[pInfo].Add(pPath);
            var tVal = serializaReference.Find(x => x.key == pInfo);
            tVal.value.Add(pPath);
        }

        void CalculateOptionName()
        {
            if (referenceDic.IsNullOrEmpty()) return;
            foreach (var item in referenceDic.Keys)
            {
                var tCount = referenceDic[item].GetCountIgnoreNull();
                if (tCount == 0) continue;
                item.optionNames = new string[tCount];
                for (int i = 0; i < tCount; i++)
                {
                    item.optionNames[i] = "定位Texture" + i;
                }
            }
        }

        public override void Draw(BaseInfo pInfo = null)
        {
            if (prefabInfos.IsNullOrEmpty() || referenceDic.IsNullOrEmpty()) return;
            EditorGUILayout.BeginVertical("box");
            foreach (var tPrefabInfo in prefabInfos)
            {
                if (!tPrefabInfo.gameObject) continue;
                tPrefabInfo.Draw(this);
                GUILayout.Space(5);
            }
            EditorGUILayout.EndVertical();
        }

        public void Select()
        {
            if (texture == null) return;
            EditorGUIUtility.PingObject(texture);
        }

        static public Transform GetTrasnformByPath(GameObject pInstance, string pName, string pPath, int pIndex = 0)
        {
            if (pInstance == null) return null;
            if (pPath.IsNullOrEmpty())
            {
                var tTexture = pInstance.GetComponent<UITexture>();
                if (CheckTextureIsEqual(pInstance.transform, tTexture, pName, pPath))
                {
                    return tTexture.transform;
                }

                var tPanel = pInstance.GetComponent<UIPanel>();
                if (CheckPanelIsEqual(tPanel, pName))
                {
                    return tPanel.transform;
                }
            }

            var tChildTextures = pInstance.transform.GetComponentsInChildren<UITexture>(true).Where(x => CheckTextureIsEqual(pInstance.transform, x, pName, pPath));
            if (tChildTextures != null && tChildTextures.Count() > 0)
            {
                if (pIndex >= 0 && pIndex < tChildTextures.Count())
                {
                    return tChildTextures.ElementAt(pIndex).transform;
                }
                else
                {
                    return tChildTextures.First().transform;
                }
            }

            var tChildPanels = pInstance.transform.GetComponentsInChildren<UIPanel>(true).Where(x => CheckPanelIsEqual(x, pName));
            if (tChildPanels != null && tChildPanels.Count() > 0)
            {
                if (pIndex >= 0 && pIndex < tChildPanels.Count())
                {
                    return tChildPanels.ElementAt(pIndex).transform;
                }
                else
                {
                    return tChildPanels.First().transform;
                }
            }

            return null;
        }

        static public bool CheckTextureIsEqual(Transform pRoot, UITexture pTex, string pName, string pPath)
        {
            if (pTex == null || pTex.mainTexture == null || pTex.mainTexture.name != pName)
            {
                return false;
            }
            if (!pPath.IsNullOrEmpty() && pTex.transform.GetHierarchyByRoot(pRoot) != pPath)
            {
                return false;
            }
            return true;
        }

        static public bool CheckPanelIsEqual(UIPanel pPanel, string pName)
        {
            if (pPanel == null || pPanel.clipping != UIDrawCall.Clipping.TextureMask || pPanel.clipTexture == null)
            {
                return false;
            }
            return true;
        }
    }

    [Serializable]
    public class BaseInfo
    {
        public string guid { protected set; get; }
        public string assetPath { protected set; get; }
        public string fullPath { protected set; get; }
        public virtual string name { protected set; get; }
        public BaseInfo(string pGuid, string pAssetPath)
        {
            guid = pGuid;
            assetPath = pAssetPath;
            fullPath = pAssetPath.IsNullOrEmpty() ? string.Empty : Path.Combine(Application.dataPath.Replace("Assets", string.Empty), pAssetPath);
        }

        public virtual void Draw(BaseInfo pInfo = null) { }
    }

    [Serializable]
    public struct SerializaTextureDictionary
    {
        public SerializaTextureDictionary(PrefabInfo pInfo, List<string> pPaths)
        {
            key = pInfo;
            value = pPaths;
        }

        public PrefabInfo key { set; get; }
        public List<string> value { set; get; }
    }

}


static class Extension
{
    static public bool IsNullOrEmpty(this ICollection pCollection)
    {
        return pCollection == null || pCollection.Count == 0;
    }

    static public bool IsNullOrEmpty(this string pString)
    {
        return string.IsNullOrEmpty(pString);
    }

    static public int GetCountIgnoreNull(this ICollection pCollection)
    {
        return pCollection == null ? 0 : pCollection.Count;
    }

    static public string GetHierarchyByRoot(this Transform pTr, Transform pRoot)
    {
        var tTrans = pTr;
        if (tTrans == null || tTrans == pRoot) return string.Empty;
        var tPath = tTrans.name;
        while (tTrans.parent != pRoot)
        {
            tTrans = tTrans.parent;
            tPath = tTrans.name + "/" + tPath;
        }
        return tPath;
    }
}