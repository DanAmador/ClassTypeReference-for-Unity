﻿namespace TypeReferences.Editor.TypeDropdown
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using SolidUtilities;
    using SolidUtilities.Editor.Helpers;
    using SolidUtilities.Extensions;
    using UnityEngine;

    /// <summary>
    /// Represents a node tree that contains folders (namespaces) and types. It is also responsible for drawing all the
    /// nodes, along with search toolbar and scrollbar.
    /// </summary>
    internal partial class SelectionTree
    {
        private readonly List<SelectionNode> _searchModeTree = new List<SelectionNode>();
        private readonly SelectionNode _root;
        private readonly NoneElement _noneElement;
        private readonly string _searchFieldControlName = Guid.NewGuid().ToString();
        private readonly Action<Type> _onTypeSelected;
        private readonly Scrollbar _scrollbar = new Scrollbar();
        private readonly int _searchbarMinItemsCount;
        private readonly int _typeItemsCount;
        private readonly bool _drawSearchbar;

        private string _searchString = string.Empty;
        private SelectionNode _selectedNode;

        public SelectionTree(
            SortedSet<TypeItem> items,
            Type selectedType,
            Action<Type> onTypeSelected,
            int searchbarMinItemsCount,
            bool hideNoneElement)
        {
            _root = SelectionNode.CreateRoot(this);
            _onTypeSelected = onTypeSelected;

            if ( ! hideNoneElement)
                _noneElement = NoneElement.Create(this);

            SelectionPaths = items.Select(item => item.Path).ToArray();
            FillTreeWithItems(items);
            _drawSearchbar = items.Count >= searchbarMinItemsCount;

            SetSelection(items, selectedType);
        }

        public event Action SelectionChanged;

        public string[] SelectionPaths { get; }

        public SelectionNode SelectedNode
        {
            get => _selectedNode;
            set
            {
                _selectedNode = value;
                _onTypeSelected(_selectedNode.Type);
                SelectionChanged?.Invoke();
            }
        }

        public bool DrawInSearchMode { get; private set; }

        private List<SelectionNode> Nodes => _root.ChildNodes;

        public void ExpandAllFolders()
        {
            foreach (SelectionNode node in EnumerateTree())
                node.Expanded = true;
        }

        public void Draw()
        {
            if (Nodes.Count == 0)
            {
                DrawInfoMessage();
                return;
            }

            if (_drawSearchbar)
                EditorDrawHelper.DrawWithSearchToolbarStyle(DrawSearchToolbar, DropdownStyle.SearchToolbarHeight);

            if ( ! DrawInSearchMode)
                _noneElement?.Draw();

            _scrollbar.DrawWithScrollbar(DrawTree);
        }

        private static void DrawInfoMessage()
        {
            DrawHelper.DrawVertically(DropdownStyle.NoPadding, () =>
            {
                EditorDrawHelper.DrawInfoMessage("No types to select.");
            });
        }

        private IEnumerable<SelectionNode> EnumerateTree() => _root.GetChildNodesRecursive();

        private void SetSelection(SortedSet<TypeItem> items, Type selectedType)
        {
            if (selectedType == null)
            {
                _noneElement?.Select();
                return;
            }

            string nameOfItemToSelect = items.First(item => item.Type == selectedType).Path;

            if (string.IsNullOrEmpty(nameOfItemToSelect))
                return;

            SelectionNode itemToSelect = _root;

            foreach (string part in nameOfItemToSelect.Split('/'))
                itemToSelect = itemToSelect.FindChild(part);

            itemToSelect.Select();
            _scrollbar.RequestScrollToNode(itemToSelect);
        }

        private void DrawSearchToolbar()
        {
            Rect innerToolbarArea = GetInnerToolbarArea();

            bool changed = EditorDrawHelper.CheckIfChanged(() =>
            {
                _searchString = DrawSearchField(innerToolbarArea, _searchString);
            });

            if ( ! changed)
                return;

            if (string.IsNullOrEmpty(_searchString))
            {
                DisableSearchMode();
            }
            else
            {
                EnableSearchMode();
            }
        }

        private void DisableSearchMode()
        {
            DrawInSearchMode = false;
            _scrollbar.RequestScrollToNode(SelectedNode);
        }

        private void EnableSearchMode()
        {
            if ( ! DrawInSearchMode)
                _scrollbar.ToTop();

            DrawInSearchMode = true;

            _searchModeTree.Clear();
            _searchModeTree.AddRange(EnumerateTree()
                .Where(node => node.Type != null)
                .Select(node =>
                {
                    bool includeInSearch = FuzzySearch.CanBeIncluded(_searchString, node.FullTypeName, out int score);
                    return new { score, item = node, include = includeInSearch };
                })
                .Where(x => x.include)
                .OrderByDescending(x => x.score)
                .Select(x => x.item));
        }

        private static Rect GetInnerToolbarArea()
        {
            Rect outerToolbarArea = GUILayoutUtility.GetRect(
                0.0f,
                DropdownStyle.SearchToolbarHeight,
                DrawHelper.ExpandWidth(true));

            Rect innerToolbarArea = outerToolbarArea
                .AddHorizontalPadding(10f, 2f)
                .AlignMiddleVertically(DropdownStyle.LabelHeight);

            return innerToolbarArea;
        }

        private void DrawTree(Rect visibleRect)
        {
            List<SelectionNode> nodes = DrawInSearchMode ? _searchModeTree : Nodes;
            int nodesListLength = nodes.Count;
            for (int index = 0; index < nodesListLength; ++index)
                nodes[index].DrawSelfAndChildren(0, visibleRect);
        }

        private string DrawSearchField(Rect innerToolbarArea, string searchText)
        {
            (Rect searchFieldArea, Rect buttonRect) = innerToolbarArea.CutVertically(DropdownStyle.IconSize, true);

            searchText = EditorDrawHelper.FocusedTextField(searchFieldArea, searchText, "Search",
                DropdownStyle.SearchToolbarStyle, _searchFieldControlName);

            if (DrawHelper.CloseButton(buttonRect))
            {
                searchText = string.Empty;
                GUI.FocusControl(null); // Without this, the old text does not disappear for some reason.
                GUI.changed = true;
            }

            return searchText;
        }
    }
}