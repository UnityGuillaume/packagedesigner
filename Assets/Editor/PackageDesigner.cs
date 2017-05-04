﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.Text;

public class PackageDesigner : EditorWindow
{
    public List<string> nonCompilingFiles { get { return m_NonCompilingScriptFiles; } }

    AssetPackage[] m_AssetPackageList;
    Vector2 m_PackageListScrollPos;

    AssetPackage m_CurrentlyEdited = null;

    DependencyTreeView m_AssetPreviewTree;
    TreeViewState m_tvstate;

    DependencyTreeView m_ReorgTreeView;
    TreeViewState m_reorgstate;

    string m_PackageCompileError;
    List<string> m_NonCompilingScriptFiles = new List<string>();
    Vector2 m_ErroDisplayScroll;

    [MenuItem("Content/PackageDesigner")]
    static void Open()
    {
        GetWindow<PackageDesigner>();
    }

    private void OnFocus()
    {
        PopulateTreeview();
    }

    private void OnEnable()
    {
        GetAllPackageList();

        m_tvstate = new TreeViewState();
        m_AssetPreviewTree = new DependencyTreeView(m_tvstate);
        m_AssetPreviewTree.designer = this;
        m_AssetPreviewTree.isPreview = true;

        m_reorgstate = new TreeViewState();
        m_ReorgTreeView = new DependencyTreeView(m_reorgstate);
        m_ReorgTreeView.designer = this;
        m_ReorgTreeView.isPreview = false;

        Undo.undoRedoPerformed += UndoPerformed;

        PopulateTreeview();
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= UndoPerformed;
    }

    void UndoPerformed()
    {
        PopulateTreeview();
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Export All Package"))
        {
            ExportAll();
        }
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(GUILayout.Width(128));
        if(GUILayout.Button("New..."))
        {
            string savePath = EditorUtility.SaveFilePanelInProject("Save package file", "package", "asset", "Save package");
            if(savePath != "")
            {
                AssetPackage package = CreateInstance<AssetPackage>();
                AssetDatabase.CreateAsset(package, savePath.Replace(Application.dataPath, "Assets"));
                m_CurrentlyEdited = package;
                ArrayUtility.Add(ref m_AssetPackageList, package);
                AssetDatabase.Refresh();
            }
        }

        m_PackageListScrollPos = EditorGUILayout.BeginScrollView(m_PackageListScrollPos, "box");

        for (int i = 0; i < m_AssetPackageList.Length; ++i)
        {
            if (m_AssetPackageList[i] != m_CurrentlyEdited)
            {
                if (GUILayout.Button(m_AssetPackageList[i].packageName))
                {
                    m_CurrentlyEdited = m_AssetPackageList[i];
                    PopulateTreeview();
                }
            }
            else
            {
                GUILayout.Label(m_AssetPackageList[i].packageName);
            }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        if (m_CurrentlyEdited != null)
            EditedUI();

        EditorGUILayout.EndHorizontal();
    }

    void ExportCurrentPackage(string saveTo = "")
    {
        if(saveTo == "")
            saveTo = EditorUtility.SaveFilePanel("Export package", Application.dataPath.Replace("/Assets", ""), m_CurrentlyEdited.packageName, "unitypackage");

        AssetDatabase.ExportPackage(m_CurrentlyEdited.dependencies, saveTo, ExportPackageOptions.Default);
    }

    void ExportAll()
    {
        string saveFolder = EditorUtility.SaveFolderPanel("Choose output Folder", Application.dataPath.Replace("/Assets", ""), "Package Ouput");
        if (saveFolder == "")
            return;

        AssetPackage current = m_CurrentlyEdited;
        for(int i = 0; i < m_AssetPackageList.Length; ++i)
        {
            m_CurrentlyEdited = m_AssetPackageList[i];
            ExportCurrentPackage(saveFolder + "/" + m_CurrentlyEdited.packageName + ".unitypackage");
        }
    }

    void EditedUI()
    {
        EditorGUILayout.BeginVertical();
        EditorGUILayout.BeginVertical();

        if (m_PackageCompileError != "")
        {
            m_ErroDisplayScroll = EditorGUILayout.BeginScrollView(m_ErroDisplayScroll, GUILayout.Height(64));
            EditorGUILayout.HelpBox(m_PackageCompileError, MessageType.Error);
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.BeginHorizontal();
        m_CurrentlyEdited.packageName = EditorGUILayout.DelayedTextField("Package Name", m_CurrentlyEdited.packageName);
        if(GUILayout.Button("Export ..."))
        {
            ExportCurrentPackage();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        //EditorGUILayout.BeginVertical();
        //GUILayout.FlexibleSpace();
        //EditorGUILayout.EndVertical();

        //if (Event.current.type == EventType.DragUpdated)
        //{
        //    Rect rect = GUILayoutUtility.GetLastRect();
        //    DragAndDrop.visualMode = rect.Contains(Event.current.mousePosition) ? DragAndDropVisualMode.Move : DragAndDropVisualMode.Rejected;
        //}
        //else if (Event.current.type == EventType.DragPerform)
        //{
        //    GetAllAssetsDependency(DragAndDrop.objectReferences);
        //}

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("ASSETS LIST", GUILayout.ExpandHeight(true));
        
        EditorGUILayout.EndVertical();
        Rect r = GUILayoutUtility.GetLastRect();
        r.y += EditorGUIUtility.singleLineHeight;
        r.height -= EditorGUIUtility.singleLineHeight * 1.5f;
        m_AssetPreviewTree.OnGUI(r);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("OUTPUT", GUILayout.ExpandHeight(true));
        EditorGUILayout.EndVertical();
        r = GUILayoutUtility.GetLastRect();
        r.y += EditorGUIUtility.singleLineHeight;
        r.height -= EditorGUIUtility.singleLineHeight * 1.5f;
        m_ReorgTreeView.OnGUI(r);

        EditorGUILayout.EndHorizontal();

        if (m_AssetPreviewTree.assetPreviews != null && m_AssetPreviewTree.assetPreviews.Length > 0)
        {
            EditorGUILayout.BeginHorizontal("box", GUILayout.Height(128.0f));
            if (m_AssetPreviewTree.assetPreviews != null)
            {
                for (int i = 0; i < m_AssetPreviewTree.assetPreviews.Length; ++i)
                {
                    EditorGUILayout.LabelField(m_AssetPreviewTree.assetPreviews[i], GUILayout.Height(128.0f));
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    void GetAllAssetsDependency(Object[] objs)
    {
        Undo.RecordObject(m_CurrentlyEdited, "Dependencies added");
        if(m_CurrentlyEdited.dependenciesID == null)
            m_CurrentlyEdited.dependenciesID = new string[0];

        for(int i = 0; i < objs.Length; ++i)
        {
            string[] str = AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(objs[i]));

            for (int j = 0; j < str.Length; ++j)
            {
                string depID = AssetDatabase.AssetPathToGUID(str[j]);
                if (!ArrayUtility.Contains(m_CurrentlyEdited.dependenciesID, depID))
                {
                    ArrayUtility.Add(ref m_CurrentlyEdited.dependenciesID, depID);
                    ArrayUtility.Add(ref m_CurrentlyEdited.outputPath, AssetDatabase.GUIDToAssetPath(depID));
                }
            }
        }
        PopulateTreeview();
    }

    public void PopulateTreeview()
    {
        m_PackageCompileError = "";
        m_ErroDisplayScroll = Vector2.zero;

        if (m_CurrentlyEdited == null)
            return;

        string[] files = new string[0];
        string[] depPath = m_CurrentlyEdited.dependencies;
        for (int i = 0; i < depPath.Length; ++i)
        {
            if(AssetDatabase.GetMainAssetTypeAtPath(depPath[i]) == typeof(MonoScript))
            {
                ArrayUtility.Add(ref files, depPath[i].Replace("Assets", Application.dataPath));
            }
        }

        if(files.Length != 0)
        {
            CheckCanCompile(files);
        }


        m_AssetPreviewTree.assetPackage = m_CurrentlyEdited;
        m_AssetPreviewTree.Reload();
        m_AssetPreviewTree.ExpandAll();

        m_ReorgTreeView.assetPackage = m_CurrentlyEdited;
        m_ReorgTreeView.Reload();
        m_ReorgTreeView.ExpandAll();
    }

    void GetAllPackageList()
    {
        string[] assets = AssetDatabase.FindAssets("t:AssetPackage");
        m_AssetPackageList = new AssetPackage[assets.Length];

        for(int i = 0; i < assets.Length; ++i)
        {
            m_AssetPackageList[i] = AssetDatabase.LoadAssetAtPath<AssetPackage>(AssetDatabase.GUIDToAssetPath(assets[i]));
        }
    }

    void CheckCanCompile(string[] files)
    {
        m_NonCompilingScriptFiles.Clear();
        CSharpCodeProvider provider = new CSharpCodeProvider();
        CompilerParameters parameters = new CompilerParameters();

        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.Location.Contains("Library"))
                continue;//we don't add the library specific to the project, as we want to test compilign for any project
            parameters.ReferencedAssemblies.Add(assembly.Location);
        }

        parameters.GenerateInMemory = true;
        parameters.GenerateExecutable = false;

        CompilerResults results = provider.CompileAssemblyFromFile(parameters, files);

        if (results.Errors.HasErrors)
        {
            StringBuilder sb = new StringBuilder();

            foreach (CompilerError error in results.Errors)
            {
                sb.AppendLine(string.Format("Error in {0} : {1}", error.FileName, error.ErrorText));
                m_NonCompilingScriptFiles.Add(error.FileName.Replace(Application.dataPath, "Assets"));
            }


            m_PackageCompileError = sb.ToString();
        }
    }
}


public class DependencyTreeView : TreeView
{
    public AssetPackage assetPackage = null;
    public PackageDesigner designer = null;
    public GUIContent[] assetPreviews = null;

    //preview tree just list asset inside the package, they allow deleting stuff (remove from package).
    //non preview tree are the one use to reorganize the assets. they don't allow deleting, but allow creating empty folder/move things around
    public bool isPreview = false;

    protected Texture2D m_FolderIcone;
    //TODO replace that with proper id handling, reusing etc. For now simple increment will be enough
    protected int freeID = 0;

    protected GenericMenu m_contextMenu;

    public DependencyTreeView(TreeViewState state) : base(state)
    {
        m_FolderIcone = EditorGUIUtility.Load("Folder Icon.asset") as Texture2D;
        freeID = 0;
    }

    protected override TreeViewItem BuildRoot()
    {
        TreeViewItem item = new TreeViewItem();
        item.depth = -1;

        if (assetPackage == null || assetPackage.dependenciesID == null || assetPackage.dependenciesID.Length == 0)
        {
            TreeViewItem itm = new TreeViewItem(freeID, 0, "Nothing");
            item.AddChild(itm);
            return item;
        }

        for(int i = 0; i < assetPackage.dependenciesID.Length; ++i)
        {
            string path, assetpath;

            assetpath = AssetDatabase.GUIDToAssetPath(assetPackage.dependenciesID[i]);
            if (isPreview)
            {
                path = assetpath;
            }
            else
                path = assetPackage.outputPath[i];

            RecursiveAdd(item, path, assetpath);
        }

        SetupDepthsFromParentsAndChildren(item);
        return item;
    }

    void RecursiveAdd(TreeViewItem root, string value, string fullPath)
    {
        int idx = value.IndexOf('/');

        if (idx > 0)
        {
            string node = value.Substring(0, idx);
            string childValue = value.Substring(idx+1);

            if (root.hasChildren)
            {
                for (int i = 0; i < root.children.Count; ++i)
                {
                    if (root.children[i].displayName == node)
                    {
                        RecursiveAdd(root.children[i], childValue, fullPath);
                        return;
                    }
                }
            }

            //we didn't find a children named that way, so create a new one
            TreeViewItem itm = new TreeViewItem(freeID);
            freeID += 1;
            itm.displayName = node;
            itm.icon = m_FolderIcone;
            root.AddChild(itm);
            RecursiveAdd(itm, childValue, fullPath);
        }
        else
        {//this is a leaf node, just create a new one and add it to the root
            AssetTreeViewItem itm = new AssetTreeViewItem(freeID);
            freeID += 1;

            itm.displayName = value;
            itm.fullAssetPath = fullPath;
            Object obj = AssetDatabase.LoadAssetAtPath(fullPath, typeof(UnityEngine.Object));
            itm.icon = AssetPreview.GetMiniThumbnail(obj);

            root.AddChild(itm);
        }
    }

    protected override void SelectionChanged(IList<int> selectedIds)
    {
        base.SelectionChanged(selectedIds);

        assetPreviews = new GUIContent[0];

        IList<TreeViewItem> items = FindRows(selectedIds);

        for(int i = 0; i < items.Count; ++i)
        {
            if(!items[i].hasChildren)
            {
                AssetTreeViewItem itm = items[i] as AssetTreeViewItem;
                if(itm != null)
                {
                    GUIContent content = new GUIContent(AssetPreview.GetAssetPreview(AssetDatabase.LoadAssetAtPath(itm.fullAssetPath, typeof(Object))));
                    ArrayUtility.Add(ref assetPreviews, content);
                }
            }
        }
    }

    protected override void ContextClickedItem(int id)
    {
        base.ContextClickedItem(id);

        if (isPreview)
            return;//preview tree have no context click

        TreeViewItem itm = FindItem(id, rootItem);

        if (itm is AssetTreeViewItem)
            return; //we can only context click folder, not assets

        m_contextMenu = new GenericMenu();
        m_contextMenu.AddItem(new GUIContent("New Folder"), false, CreateFolderContext, itm);
        m_contextMenu.ShowAsContext();
    }

    protected void CreateFolderContext(object item)
    {
        TreeViewItem root = item as TreeViewItem;
        TreeViewItem itm = new TreeViewItem(freeID);
        freeID += 1;
        itm.displayName = "Folder";
        itm.icon = m_FolderIcone;
        root.AddChild(itm);

        SetupDepthsFromParentsAndChildren(root);
        SetExpanded(itm.id, true);
    }

    protected override void CommandEventHandling()
    {
        base.CommandEventHandling();

        if(Event.current.commandName.Contains("Delete"))
        {
            IList<TreeViewItem> items = FindRows(GetSelection());
            bool haveDelete = false;

            Undo.RecordObject(assetPackage, "Delete dependency");
            for (int i = 0; i < items.Count; ++i)
            {
                RecursiveDelete(items[i], ref haveDelete);
            }

            if (haveDelete)
            {
                DesignerReloadTrees();
            }
        }
    }

    void RecursiveDelete(TreeViewItem item, ref bool haveDelete)
    {
        if (!item.hasChildren)
        {
            AssetTreeViewItem itm = item as AssetTreeViewItem;
            if (itm != null)
            {
                if (!haveDelete)
                {
                    haveDelete = true;
                }

                string guid = AssetDatabase.AssetPathToGUID(itm.fullAssetPath);
                int idx = ArrayUtility.FindIndex(assetPackage.dependenciesID, x => x == guid);

                if (idx != -1)
                {
                    ArrayUtility.RemoveAt(ref assetPackage.dependenciesID, idx);
                    ArrayUtility.RemoveAt(ref assetPackage.outputPath, idx);
                }
            }
        }
        else
        {
            for (int j = 0; j < item.children.Count; ++j)
            {
                RecursiveDelete(item.children[j], ref haveDelete);
            }
        }
    }

    protected override void RowGUI(RowGUIArgs args)
    {
        GUI.color = Color.white;

        if (args.item.hasChildren == false)
        {
            AssetTreeViewItem itm = args.item as AssetTreeViewItem;
            if (itm != null)
            {
                if(designer.nonCompilingFiles.Contains(itm.fullAssetPath))
                {
                    GUI.color = Color.red;
                }
            }
        }

        base.RowGUI(args);
    }

    protected override bool CanStartDrag(CanStartDragArgs args)
    {
        return !isPreview;
    }

    protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
    {
        base.SetupDragAndDrop(args);

        m_Dragged.Clear();
        m_Dragged.AddRange(args.draggedItemIDs);

        DragAndDrop.PrepareStartDrag();
        DragAndDrop.StartDrag("Item view");
        //DragAndDrop.visualMode = DragAndDropVisualMode.Move;
    }

    List<int> m_Dragged = new List<int>();
    protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
    {
        if (args.dragAndDropPosition == DragAndDropPosition.OutsideItems)
            return DragAndDropVisualMode.Rejected;

        if(args.performDrop)
        {
            TreeViewItem item = args.parentItem;

            if (item is AssetTreeViewItem)
                return DragAndDropVisualMode.Rejected;

            
            for(int i = 0; i < m_Dragged.Count; ++i)
            {
                TreeViewItem itm = FindItem(m_Dragged[i], rootItem);
                //itm.
            }
        }

        return DragAndDropVisualMode.Move;
    }

    void DesignerReloadTrees()
    {
        designer.PopulateTreeview();
    }
}

public class AssetTreeViewItem : TreeViewItem
{
    public string fullAssetPath = "";

    public AssetTreeViewItem(int id) : base(id) { }
}