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
        var tHasWindow = Resources.FindObjectsOfTypeAll<EditorWindow>().ToList().Find(x => x.GetType() == typeof(NGUITextureCheck)) != null;
        ShowWindow();
        if (!tHasWindow) sInstance.CheckTextureReference();
        sInstance.mSearchText = pTextureName;
        sInstance.Focus();
    }

    public enum SortType
    {
        Name = 1,
        Count,
        Size,
    }

    public enum ViewType
    {
        /// <summary>
        /// 图片引用界面
        /// </summary>
        Texture,
        /// <summary>
        /// 图集引用界面
        /// </summary>
        Atlas,
    }

    public const string cPrefabPath = "Assets/UI/Prefabs";
    public const string cTexturePath = "Assets/UI/Atlas/CommonTextures";
    public const string cAtlasPath = "Assets/UI/Atlas";
    public static string[] sSortTypeNames = new string[]
    {
        "",
        "名字",
        "数量",
        "尺寸"
    };
    public static string[] sViewTypeNames = new string[]
    {
        "图片引用",
        "图集引用"
    };
    static Dictionary<ViewType, Vector2> sScrollViewPosDic = new Dictionary<ViewType, Vector2>();

    List<TextureInfo> mTextureInfos;
    List<AtlasInfo> mAtlasInfos;
    SortType mSortType = SortType.Name;
    string mSearchText;
    SortType mLastSortTyp = 0;
    bool mDescending;
    bool mInitTexture;
    bool mInitAtlas;
    bool mMatchFullName;
    ViewType mViewType;

    /// <summary>
    /// 检测所有Texture引用
    /// </summary>
    void CheckTextureReference()
    {
        var tPrefabInfos = new List<PrefabInfo>();
        foreach (var item in GetAllAssetsByPath(new string[] { cPrefabPath }, "t:prefab", "Prefab数据初始化"))
        {
            tPrefabInfos.Add(new PrefabInfo(item));
        }

        mTextureInfos = new List<TextureInfo>();
        foreach (var item in GetAllAssetsByPath(new string[] { cTexturePath }, "t:texture", "Texture数据初始化"))
        {
            mTextureInfos.Add(new TextureInfo(item));
        }

        mTextureInfos = new List<TextureInfo>();
        foreach (var item in GetAllAssetsByPath(new string[] { cTexturePath }, "t:texture", "Texture数据初始化"))
        {
            mTextureInfos.Add(new TextureInfo(item));
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
        mInitTexture = true;
    }

    /// <summary>
    /// 检测所有图集引用
    /// </summary>
    void CheckAtlasReference()
    {
        var tPrefabInfos = new List<PrefabInfo>();
        foreach (var item in GetAllAssetsByPath(new string[] { cPrefabPath }, "t:prefab", "Prefab数据初始化"))
        {
            tPrefabInfos.Add(new PrefabInfo(item));
        }

        mAtlasInfos = new List<AtlasInfo>();
        foreach (var item in GetAllAssetsByPath(new string[] { cAtlasPath }, "t:prefab", "Atlas数据初始化"))
        {
            mAtlasInfos.Add(new AtlasInfo(item));
        }

        if (!tPrefabInfos.IsNullOrEmpty())
        {
            for (int i = 0, imax = mAtlasInfos.Count; i < imax; i++)
            {
                var tInfo = mAtlasInfos[i];
                var tPrefabs = tPrefabInfos.FindAll(x => x.ExistGuid(tInfo.guid));
                if (tPrefabs.IsNullOrEmpty()) continue;
                tInfo.UpdateReference(tPrefabInfos);
                EditorUtility.DisplayProgressBar(string.Empty, "查找Atlas引用", i / (float)imax);
            }
        }
        EditorUtility.ClearProgressBar();
        mInitAtlas = true;
    }

    public struct AssetInfo
    {
        public Object info { set; get; }
        public string guid { set; get; }
        public string path { set; get; }

        public AssetInfo(Object pInfo, string pGuid, string pPath)
        {
            info = pInfo;
            guid = pGuid;
            path = pPath;
        }
    }

    IEnumerable<AssetInfo> GetAllAssetsByPath(string[] pPaths, string pFilter, string pProgressText)
    {
        var tGuids = AssetDatabase.FindAssets(pFilter, pPaths);
        var tPrefabInfos = new List<PrefabInfo>(tGuids.Length);
        for (int i = 0, imax = tGuids.Length; i < imax; i++)
        {
            var tGuid = tGuids[i];
            var tPath = AssetDatabase.GUIDToAssetPath(tGuid);
            var tAsset = AssetDatabase.LoadAssetAtPath<Object>(tPath);
            if (tAsset == null) continue;
            EditorUtility.DisplayProgressBar(string.Empty, pProgressText, i / (float)imax);
            yield return new AssetInfo(tAsset, tGuid, tPath);
        }
    }

    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        mViewType = (ViewType)EditorGUILayout.Popup((int)mViewType, sViewTypeNames, GUILayout.Width(150));
        EditorGUILayout.LabelField("排序类型", GUILayout.Width(50));
        mSortType = (SortType)EditorGUILayout.Popup((int)mSortType, sSortTypeNames, GUILayout.Width(50));
        if (GUILayout.Button(mDescending ? "顺序" : "逆序", GUILayout.Width(50)))
        {
            mLastSortTyp = 0;
            mDescending = !mDescending;
        }
        GUI.changed = false;
        using (var s = new EditorGUILayout.HorizontalScope())
        {
            mSearchText = EditorGUILayout.TextField(string.Empty, mSearchText, "SearchTextField", GUILayout.MinWidth(50));
            if (GUILayout.Button(mSearchText, mSearchText.IsNullOrEmpty() ? "SearchCancelButtonEmpty" : "SearchCancelButton"))
            {
                if (!mSearchText.IsNullOrEmpty())
                {
                    mSearchText = string.Empty;
                    GUI.FocusControl(string.Empty);
                }
            }
        }

        if (GUI.changed) mMatchFullName = false;
        EditorGUILayout.EndHorizontal();

        if (mSortType != mLastSortTyp)
        {
            mLastSortTyp = mSortType;
            switch (mViewType)
            {
                case ViewType.Texture:
                    TextureInfo.Sort(mTextureInfos, mSortType, mDescending);
                    break;
                case ViewType.Atlas:
                    AtlasInfo.Sort(mAtlasInfos, mSortType, mDescending);
                    break;
            }
        }
        switch (mViewType)
        {
            case ViewType.Texture:
                DrawTextureView();
                break;
            case ViewType.Atlas:
                DrawAtlasView();
                break;
        }
    }

    void DrawTextureView()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.HelpBox(string.Format("拖拽{0}下的Texture到该窗口下可以快速查看该Texture的引用", cTexturePath), MessageType.Info);
        if (GUILayout.Button("检测所有Texture引用", GUILayout.Height(40)))
        {
            CheckTextureReference();
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(10);

        if (mTextureInfos.IsNullOrEmpty())
        {
            if (Event.current.type == EventType.DragUpdated)
            {
                if (!mInitTexture) CheckTextureReference();
            }
            return;
        }

        if (!sScrollViewPosDic.ContainsKey(mViewType)) sScrollViewPosDic.Add(mViewType, Vector2.zero);
        sScrollViewPosDic[mViewType] = EditorGUILayout.BeginScrollView(sScrollViewPosDic[mViewType]);
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

        if (GUI.Button(new Rect(Vector2.zero, position.size), string.Empty, GUIStyle.none))
        {
            GUI.FocusControl(string.Empty);
        }
    }

    void DrawAtlasView()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("检测所有Atlas引用", GUILayout.Height(40)))
        {
            CheckAtlasReference();
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(10);

        if (mAtlasInfos.IsNullOrEmpty())
        {
            if (Event.current.type == EventType.DragUpdated)
            {
                if (!mInitAtlas) CheckAtlasReference();
            }
            return;
        }

        if (!sScrollViewPosDic.ContainsKey(mViewType)) sScrollViewPosDic.Add(mViewType, Vector2.zero);
        sScrollViewPosDic[mViewType] = EditorGUILayout.BeginScrollView(sScrollViewPosDic[mViewType]);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(10);
        EditorGUILayout.BeginVertical();
        for (int i = 0; i < mAtlasInfos.Count; i++)
        {
            var item = mAtlasInfos[i];
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

    /// <summary>
    /// 图集信息
    /// </summary>
    [Serializable]
    public class AtlasInfo : PrefabInfo
    {
        public AtlasInfo(AssetInfo pInfo) : base((GameObject)pInfo.info, pInfo.guid, pInfo.path)
        {
            atlas = gameObject == null ? null : gameObject.GetComponent<UIAtlas>();
            texture = atlas.texture;
        }

        Dictionary<PrefabInfo, List<string>> mReferenceDic;

        public UIAtlas atlas { private set; get; }
        public Texture texture { private set; get; }
        public override string guiName
        {
            get
            {
                return string.Format("name:{0} count:{1}", name, prefabInfos.GetCountIgnoreNull());
            }
        }
        public List<string> spriteNames
        {
            get
            {
                return atlas == null ? null : atlas.spriteList.ConvertAll(x => x.name);
            }
        }
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
                var tSprite = tRoot.GetComponent<UISprite>();
                if (CheckSpriteIsEqual(tRoot, tSprite, this, string.Empty))
                {
                    AddPathToDic(tPrefabInfo, string.Empty);
                }

                var tChildSprites = tRoot.GetComponentsInChildren<UISprite>(true);
                foreach (var tChildSprite in tChildSprites)
                {
                    if (!CheckSpriteIsEqual(tRoot, tChildSprite, this, string.Empty)) continue;
                    AddPathToDic(tPrefabInfo, tChildSprite.transform.GetHierarchyByRoot(tRoot));
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
                    item.optionNames[i] = "定位Sprite" + i;
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

        static public bool operator !=(AtlasInfo pInfo, UISprite pSprite)
        {
            if (pSprite == null) return true;
            return pInfo.atlas != pSprite.atlas || pInfo.spriteNames.IsNullOrEmpty() || !pInfo.spriteNames.Contains(pSprite.spriteName);
        }

        static public bool operator ==(AtlasInfo pInfo, UISprite pSprite)
        {
            if (pSprite == null) return false;
            return pInfo.atlas == pSprite.atlas && !pInfo.spriteNames.IsNullOrEmpty() && pInfo.spriteNames.Contains(pSprite.spriteName);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        static public bool CheckSpriteIsEqual(Transform pRoot, UISprite pSprite, AtlasInfo pInfo, string pPath)
        {
            if (pInfo != pSprite)
            {
                return false;
            }
            if (!pPath.IsNullOrEmpty() && pSprite.transform.GetHierarchyByRoot(pRoot) != pPath)
            {
                return false;
            }
            return true;
        }

        static public void Sort(List<AtlasInfo> pInfos, SortType pSortType, bool pDescending)
        {
            if (pInfos.IsNullOrEmpty()) return;
            switch (pSortType)
            {
                case SortType.Name:
                    pInfos.Sort((x, y) =>
                    {
                        var xl = x.name.Length;
                        var yl = y.name.Length;
                        if (xl != yl) return xl.CompareTo(yl) * (pDescending ? -1 : 1);

                        var xs = x.name.ToLower().ToCharArray();
                        var ys = y.name.ToLower().ToCharArray();
                        for (int i = 0, imax = Mathf.Min(xs.Length, ys.Length); i < imax; i++)
                        {
                            if (xs[i] != ys[i]) return xs[i].CompareTo(ys[i]) * (pDescending ? -1 : 1);
                        }
                        return x.name.CompareTo(y.name) * (pDescending ? -1 : 1);
                    });
                    break;
                case SortType.Count:
                    pInfos.Sort((x, y) =>
                    {
                        var xl = x.prefabInfos.GetCountIgnoreNull();
                        var yl = y.prefabInfos.GetCountIgnoreNull();
                        return xl.CompareTo(yl) * (pDescending ? -1 : 1);
                    });
                    break;
                case SortType.Size:
                    throw new NotImplementedException("未实现");
                    break;
            }
        }

        static public Transform GetTrasnformByPath(GameObject pInstance, AtlasInfo pInfo, string pPath, int pIndex = 0)
        {
            if (pInstance == null) return null;
            if (pPath.IsNullOrEmpty())
            {
                var tSprite = pInstance.GetComponent<UISprite>();
                if (CheckSpriteIsEqual(pInstance.transform, tSprite, pInfo, pPath))
                {
                    return tSprite.transform;
                }
            }

            var tChildSprites = pInstance.transform.GetComponentsInChildren<UISprite>(true).Where(x => CheckSpriteIsEqual(pInstance.transform, x, pInfo, pPath));
            if (tChildSprites != null && tChildSprites.Count() > 0)
            {
                if (pIndex >= 0 && pIndex < tChildSprites.Count())
                {
                    return tChildSprites.ElementAt(pIndex).transform;
                }
                else
                {
                    return tChildSprites.First().transform;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 预制信息
    /// </summary>
    [Serializable]
    public class PrefabInfo : BaseInfo
    {
        public PrefabInfo(GameObject pGo, string pGuid, string pAssetPath) : base(pGuid, pAssetPath)
        {
            gameObject = pGo;
            mContent = File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
        }

        public PrefabInfo(AssetInfo pInfo) : this(pInfo.info as GameObject, pInfo.guid, pInfo.path)
        {
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

        public override void Select()
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
                        const string tRootName = "UI Root";
                        var tRoot = GameObject.Find(tRootName);
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
                            else
                            {
                                Debug.LogErrorFormat("实例化 {0} 错误", gameObject.name);
                            }
                        }
                        else
                        {
                            Debug.LogErrorFormat("找不到 {0}", tRootName);
                        }
                    }

                    if (tGo != null)
                    {
                        tGo.name = name;

                        var tFind = TextureInfo.GetTrasnformByPath(tGo, pInfo, tPath, selectOptionIndex);
                        if (tFind != null) Selection.activeTransform = tFind;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        public void Draw(AtlasInfo pInfo)
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
                if (GUI.changed || (tReferenceCount == 1 && GUILayout.Button("定位Sprite", GUILayout.Width(90))))
                {
                    if (selectOptionIndex < 0 || selectOptionIndex >= tReferenceCount) selectOptionIndex = 0;
                    var tPath = tReferenceCount == 0 ? string.Empty : tPaths[selectOptionIndex];

                    var tGo = GameObject.Find(name);
                    if (tGo == null)
                    {
                        const string tRootName = "UI Root";
                        var tRoot = GameObject.Find("UI Root");
                        if (tRoot != null)
                        {
                            Debug.Log(gameObject.name, gameObject);
                            tGo = PrefabUtility.InstantiatePrefab(gameObject) as GameObject;
                            if (tGo != null)
                            {
                                tGo.transform.SetParent(tRoot.transform);
                                tGo.transform.localPosition = Vector3.zero;
                                tGo.transform.localRotation = Quaternion.identity;
                                tGo.transform.localScale = Vector3.one;
                            }
                            else
                            {
                                Debug.LogErrorFormat("实例化 {0} 错误", gameObject.name);
                            }
                        }
                        else
                        {
                            Debug.LogErrorFormat("找不到 {0}", tRootName);
                        }
                    }

                    if (tGo != null)
                    {
                        tGo.name = name;

                        var tFind = AtlasInfo.GetTrasnformByPath(tGo, pInfo, tPath, selectOptionIndex);
                        if (tFind != null) Selection.activeTransform = tFind;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    /// <summary>
    /// 图片信息
    /// </summary>
    [Serializable]
    public class TextureInfo : BaseInfo
    {
        public TextureInfo(Texture2D pTexture, string pGuid, string pAssetPath) : base(pGuid, pAssetPath)
        {
            texture = pTexture;
            size = texture == null ? Vector2.zero : new Vector2(texture.width, texture.height);
        }
        public TextureInfo(AssetInfo pInfo) : this(pInfo.info as Texture2D, pInfo.guid, pInfo.path)
        {
        }

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
        public override string guiName
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
                if (CheckTextureIsEqual(tRoot, tTexture, this, string.Empty))
                {
                    AddPathToDic(tPrefabInfo, string.Empty);
                }

                var tPanel = tRoot.GetComponent<UIPanel>();
                if (CheckPanelIsEqual(tPanel, this))
                {
                    AddPathToDic(tPrefabInfo, string.Empty);
                }

                var tChildTextures = tRoot.GetComponentsInChildren<UITexture>(true);
                foreach (var tChildTexture in tChildTextures)
                {
                    if (!CheckTextureIsEqual(tRoot, tChildTexture, this, string.Empty)) continue;
                    AddPathToDic(tPrefabInfo, tChildTexture.transform.GetHierarchyByRoot(tRoot));
                }

                var tChildPanels = tRoot.GetComponentsInChildren<UIPanel>(true);
                foreach (var tChildPanel in tChildPanels)
                {
                    if (!CheckPanelIsEqual(tChildPanel, this)) continue;
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

        public override void Select()
        {
            if (texture == null) return;
            EditorGUIUtility.PingObject(texture);
        }

        static public bool operator !=(TextureInfo pInfo, Object pObject)
        {
            if (pObject is UITexture)
            {
                var tTexture = pObject as UITexture;
                return tTexture == null || tTexture.mainTexture == null || pInfo.name != tTexture.mainTexture.name;
            }
            else if (pObject is UIPanel)
            {
                var tPanel = pObject as UIPanel;
                return tPanel == null || tPanel.clipping != UIDrawCall.Clipping.TextureMask || tPanel.clipTexture == null || tPanel.clipTexture.name != pInfo.name;
            }
            return true;
        }

        static public bool operator ==(TextureInfo pInfo, Object pObject)
        {
            if (pObject is UITexture)
            {
                var tTexture = pObject as UITexture;
                return tTexture != null && tTexture.mainTexture != null && pInfo.name == tTexture.mainTexture.name;
            }
            else if (pObject is UIPanel)
            {
                var tPanel = pObject as UIPanel;
                return tPanel != null && tPanel.clipping == UIDrawCall.Clipping.TextureMask && tPanel.clipTexture != null && tPanel.clipTexture.name == pInfo.name;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        static public Transform GetTrasnformByPath(GameObject pInstance, TextureInfo pInfo, string pPath, int pIndex = 0)
        {
            if (pInstance == null) return null;
            if (pPath.IsNullOrEmpty())
            {
                var tTexture = pInstance.GetComponent<UITexture>();
                if (CheckTextureIsEqual(pInstance.transform, tTexture, pInfo, pPath))
                {
                    return tTexture.transform;
                }

                var tPanel = pInstance.GetComponent<UIPanel>();
                if (CheckPanelIsEqual(tPanel, pInfo))
                {
                    return tPanel.transform;
                }
            }

            var tChildTextures = pInstance.transform.GetComponentsInChildren<UITexture>(true).Where(x => CheckTextureIsEqual(pInstance.transform, x, pInfo, pPath));
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

            var tChildPanels = pInstance.transform.GetComponentsInChildren<UIPanel>(true).Where(x => CheckPanelIsEqual(x, pInfo));
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

        static public bool CheckTextureIsEqual(Transform pRoot, UITexture pTex, TextureInfo pInfo, string pPath)
        {
            if (pInfo != pTex)
            {
                return false;
            }
            if (!pPath.IsNullOrEmpty() && pTex.transform.GetHierarchyByRoot(pRoot) != pPath)
            {
                return false;
            }
            return true;
        }

        static public bool CheckPanelIsEqual(UIPanel pPanel, TextureInfo pInfo)
        {
            if (pInfo != pPanel)
            {
                return false;
            }
            return true;
        }

        static public void Sort(List<TextureInfo> pInfos, SortType pSortType, bool pDescending)
        {
            if (pInfos.IsNullOrEmpty()) return;
            switch (pSortType)
            {
                case SortType.Name:
                    pInfos.Sort((x, y) =>
                    {
                        var xl = x.name.Length;
                        var yl = y.name.Length;
                        if (xl != yl) return xl.CompareTo(yl) * (pDescending ? -1 : 1);

                        var xs = x.name.ToLower().ToCharArray();
                        var ys = y.name.ToLower().ToCharArray();
                        for (int i = 0, imax = Mathf.Min(xs.Length, ys.Length); i < imax; i++)
                        {
                            if (xs[i] != ys[i]) return xs[i].CompareTo(ys[i]) * (pDescending ? -1 : 1);
                        }
                        return x.name.CompareTo(y.name) * (pDescending ? -1 : 1);
                    });
                    break;
                case SortType.Count:
                    pInfos.Sort((x, y) =>
                    {
                        var xl = x.prefabInfos.GetCountIgnoreNull();
                        var yl = y.prefabInfos.GetCountIgnoreNull();
                        return xl.CompareTo(yl) * (pDescending ? -1 : 1);
                    });
                    break;
                case SortType.Size:
                    pInfos.Sort((x, y) =>
                    {
                        var xs = x.size.x * x.size.y;
                        var ys = y.size.x * y.size.y;
                        return xs.CompareTo(ys) * (pDescending ? -1 : 1);
                    });
                    break;
            }
        }
    }

    /// <summary>
    /// 基类
    /// </summary>
    [Serializable]
    public class BaseInfo
    {
        public string guid { protected set; get; }
        public string assetPath { protected set; get; }
        public string fullPath { protected set; get; }
        public virtual string name { protected set; get; }
        public bool toggle { set; get; }
        public virtual string guiName { private set; get; }

        public BaseInfo(string pGuid, string pAssetPath)
        {
            guid = pGuid;
            assetPath = pAssetPath;
            fullPath = pAssetPath.IsNullOrEmpty() ? string.Empty : Path.Combine(Application.dataPath.Replace("Assets", string.Empty), pAssetPath);
        }

        public virtual void Draw(BaseInfo pInfo = null) { }
        public virtual void Select() { }
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