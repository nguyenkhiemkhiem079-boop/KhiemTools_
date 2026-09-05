using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace KhimTools.FamilyManager.Models
{
    /// <summary>
    /// Represents a logical group of families (e.g., Structure, Rebar, Annotation).
    /// Enforces the UI rule:
    /// - Structure: IsSelective = true, tri-state IsChecked (Checked, Unchecked, Indeterminate), individual child selection.
    /// - Rebar: IsSelective = false, strictly bi-state IsChecked (Checked, Unchecked). Checking queues the entire library.
    /// </summary>
    public class FamilyGroupModel : INotifyPropertyChanged
    {
        private FamilyGroupType _groupType;
        private string _displayName;
        private string _description;
        private bool? _isChecked = false;
        private bool _isUpdatingInternally = false;
        private ObservableCollection<FamilyItemModel> _families = new ObservableCollection<FamilyItemModel>();

        public FamilyGroupType GroupType
        {
            get => _groupType;
            set
            {
                if (_groupType != value)
                {
                    _groupType = value;
                    OnPropertyChanged(nameof(GroupType));
                    OnPropertyChanged(nameof(IsSelective));
                    OnPropertyChanged(nameof(RuleDescription));
                }
            }
        }

        public string DisplayName
        {
            get => _displayName;
            set { if (_displayName != value) { _displayName = value; OnPropertyChanged(nameof(DisplayName)); } }
        }

        public string Description
        {
            get => _description;
            set { if (_description != value) { _description = value; OnPropertyChanged(nameof(Description)); } }
        }

        /// <summary>
        /// True if child families can be selectively checked/unchecked individually (Structure, MEP, Arch).
        /// False for Rebar, which is loaded as an all-or-nothing complete library.
        /// </summary>
        public bool IsSelective => GroupType != FamilyGroupType.Rebar;

        public string RuleDescription => IsSelective
            ? "Selective loading: select individual families or use parent tri-state checkbox."
            : "Complete library: checking loads all discovered Rebar shape families at once.";

        /// <summary>
        /// Tri-state for selective groups (true, false, null/indeterminate).
        /// Bi-state for Rebar (true, false).
        /// </summary>
        public bool? IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    // Enforce bi-state for Rebar
                    if (!IsSelective && value == null)
                    {
                        value = false;
                    }

                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));

                    if (!_isUpdatingInternally && value.HasValue)
                    {
                        SetAllChildren(value.Value);
                    }
                }
            }
        }

        public ObservableCollection<FamilyItemModel> Families
        {
            get => _families;
            set
            {
                if (_families != null)
                {
                    _families.CollectionChanged -= OnFamiliesCollectionChanged;
                    foreach (var item in _families)
                    {
                        item.PropertyChanged -= OnChildPropertyChanged;
                    }
                }

                _families = value;

                if (_families != null)
                {
                    _families.CollectionChanged += OnFamiliesCollectionChanged;
                    foreach (var item in _families)
                    {
                        item.PropertyChanged += OnChildPropertyChanged;
                    }
                }

                OnPropertyChanged(nameof(Families));
                UpdateParentState();
                UpdateSummary();
            }
        }

        public int TotalCount => Families?.Count ?? 0;
        public int LoadedCount => Families?.Count(f => f.IsLoadedInProject) ?? 0;
        public int SelectedCount => Families?.Count(f => f.IsSelected) ?? 0;

        public string SummaryText
        {
            get
            {
                if (Families == null || Families.Count == 0) return "0 items";
                return $"{LoadedCount}/{TotalCount} in project ({SelectedCount} selected)";
            }
        }

        public FamilyGroupModel(FamilyGroupType groupType, string displayName, string description = null)
        {
            GroupType = groupType;
            DisplayName = displayName;
            Description = description;
            _families.CollectionChanged += OnFamiliesCollectionChanged;
        }

        public void SetAllChildren(bool isSelected)
        {
            _isUpdatingInternally = true;
            try
            {
                foreach (var item in _families)
                {
                    item.IsSelected = isSelected;
                }
            }
            finally
            {
                _isUpdatingInternally = false;
            }
            UpdateSummary();
        }

        public void UpdateParentState()
        {
            if (_isUpdatingInternally || Families == null || Families.Count == 0)
            {
                if (Families == null || Families.Count == 0)
                {
                    _isChecked = false;
                    OnPropertyChanged(nameof(IsChecked));
                }
                return;
            }

            int total = Families.Count;
            int selected = Families.Count(f => f.IsSelected);

            _isUpdatingInternally = true;
            try
            {
                if (IsSelective)
                {
                    if (selected == 0)
                        _isChecked = false;
                    else if (selected == total)
                        _isChecked = true;
                    else
                        _isChecked = null; // Indeterminate tri-state
                }
                else
                {
                    // Rebar is strictly bi-state
                    _isChecked = selected == total && total > 0;
                }
                OnPropertyChanged(nameof(IsChecked));
            }
            finally
            {
                _isUpdatingInternally = false;
            }
            UpdateSummary();
        }

        public void UpdateSummary()
        {
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(LoadedCount));
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(SummaryText));
        }

        private void OnFamiliesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (FamilyItemModel item in e.OldItems)
                {
                    item.PropertyChanged -= OnChildPropertyChanged;
                }
            }
            if (e.NewItems != null)
            {
                foreach (FamilyItemModel item in e.NewItems)
                {
                    item.PropertyChanged += OnChildPropertyChanged;
                }
            }
            UpdateParentState();
        }

        private void OnChildPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FamilyItemModel.IsSelected))
            {
                UpdateParentState();
            }
            else if (e.PropertyName == nameof(FamilyItemModel.Status) || e.PropertyName == nameof(FamilyItemModel.IsLoadedInProject))
            {
                UpdateSummary();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
