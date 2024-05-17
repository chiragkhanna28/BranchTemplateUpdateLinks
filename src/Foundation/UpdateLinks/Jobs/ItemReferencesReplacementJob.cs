using Sitecore;
using Sitecore.Abstractions;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Jobs;
using Sitecore.Links;
using Sitecore.StringExtensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UpdateLinks.Jobs
{
    public class ItemReferencesReplacementJob 
    {
        private readonly Database database;
        private readonly Item sourceRoot;
        private readonly int sourceRootLength;
        private readonly Item copyRoot;
        private readonly bool deep;
        private readonly ID[] fieldIDs = Array.Empty<ID>();
        private Dictionary<ID, ID> mapOriginalAndClonedItems;

        /// <summary>
        /// Construct ReferenceReplacementJob object with source and copy item
        /// </summary>
        /// <param name="source">Item object</param>
        /// <param name="copy">Item object</param>
        public ItemReferencesReplacementJob(Item source, Item copy)
          : this(source, copy, true, (ID[])null)
        {
            Assert.ArgumentNotNull((object)source, nameof(source));
            Assert.ArgumentNotNull((object)copy, nameof(copy));
        }

        /// <summary>
        /// Construct ReferenceReplacementJob object with source and copy item
        /// </summary>
        /// <param name="source">Item object</param>
        /// <param name="copy">Item object</param>
        /// <param name="deep">If <c>true</c>, the job processes all descendant items; otherwise, the job processes only given item</param>
        /// <param name="fieldIDs">Array of field IDs to only replace references in; otherwise, all item fields are processed</param>
        public ItemReferencesReplacementJob(Item source, Item copy, bool deep, ID[] fieldIDs)
        {
            Assert.ArgumentNotNull((object)source, nameof(source));
            Assert.ArgumentNotNull((object)copy, nameof(copy));
            int length = source.Paths.FullPath.Length;
            Database database = source.Database;
            Assert.IsTrue(database == copy.Database, "items from different databases");
            this.database = database;
            this.sourceRoot = source;
            this.sourceRootLength = length;
            this.copyRoot = copy;
            this.deep = deep;
            this.fieldIDs = fieldIDs ?? Array.Empty<ID>();
        }

        /// <summary>
        /// Construct ReferenceReplacementJob object with database, sourceid and copyid
        /// </summary>
        /// <param name="database">Database</param>
        /// <param name="sourceId">Source ID</param>
        /// <param name="copyId">Copy ID</param>
        public ItemReferencesReplacementJob(Database database, string sourceId, string copyId)
          : this(database, sourceId, copyId, true, (string[])null)
        {
            Assert.ArgumentNotNull((object)database, nameof(database));
            Assert.ArgumentNotNull((object)sourceId, nameof(sourceId));
            Assert.ArgumentNotNull((object)copyId, nameof(copyId));
        }

        /// <summary>
        /// Construct ReferenceReplacementJob object with database, sourceid and copyid
        /// </summary>
        /// <param name="database">Database</param>
        /// <param name="sourceId">Source ID</param>
        /// <param name="copyId">Copy ID</param>
        /// <param name="deep">If <c>true</c>, the job processes all descendant items; otherwise, the job processes only given item</param>
        /// <param name="fieldIDs">Array of field IDs to only replace references in; otherwise, all item fields are processed</param>
        public ItemReferencesReplacementJob(
          Database database,
          string sourceId,
          string copyId,
          bool deep,
          string[] fieldIDs)
        {
            Assert.ArgumentNotNull((object)database, nameof(database));
            Assert.ArgumentNotNull((object)sourceId, nameof(sourceId));
            Assert.ArgumentNotNull((object)copyId, nameof(copyId));
            Item obj1 = database.GetItem(sourceId);
            Assert.IsNotNull((object)obj1, "Source item {0} does not exist in {1} database".FormatWith((object)sourceId, (object)database.Name));
            Item obj2 = database.GetItem(copyId);
            Assert.IsNotNull((object)obj2, "Target item {0} does not exist in {1} database".FormatWith((object)copyId, (object)database.Name));
            int length = obj1.Paths.FullPath.Length;
            this.database = obj1.Database;
            this.sourceRoot = obj1;
            this.sourceRootLength = length;
            this.copyRoot = obj2;
            this.deep = deep;
            this.fieldIDs = ((IEnumerable<string>)(fieldIDs ?? Array.Empty<string>())).Select<string, ID>((Func<string, ID>)(x => ID.Parse(x))).ToArray<ID>();
        }

        /// <summary>
        /// Construct ReferenceReplacementJob object with source and copy item
        /// </summary>
        /// <param name="database">Database</param>
        /// <param name="sourceId">Source ID</param>
        /// <param name="copyId">Copy ID</param>
        public ItemReferencesReplacementJob(Database database, ID sourceId, ID copyId)
        {
            Assert.ArgumentNotNull((object)database, nameof(database));
            Assert.ArgumentNotNull((object)sourceId, nameof(sourceId));
            Assert.ArgumentNotNull((object)copyId, nameof(copyId));
            Item obj1 = database.GetItem(sourceId);
            Assert.IsNotNull((object)obj1, "Source item {0} does not exist in {1} database".FormatWith((object)sourceId, (object)database.Name));
            Item obj2 = database.GetItem(copyId);
            Assert.IsNotNull((object)obj2, "Target item {0} does not exist in {1} database".FormatWith((object)copyId, (object)database.Name));
            int length = obj1.Paths.FullPath.Length;
            this.database = obj1.Database;
            this.sourceRoot = obj1;
            this.sourceRootLength = length;
            this.copyRoot = obj2;
        }

        /// <summary>Start job asynchronously</summary>
        public virtual void StartAsync()
        {
            this.PreProcessClonedChild(this.copyRoot);
            string name = typeof(ItemReferencesReplacementJob).Name;
            string siteName = Context.Site == null ? "shell" : Context.Site.Name;
            JobManager.Start((BaseJobOptions)new DefaultJobOptions(string.Format("ReferenceReplacement_{0}_{1}_{2}.", (object)this.sourceRoot.Database.Name, (object)this.sourceRoot.ID.ToShortID(), (object)this.copyRoot.ID.ToShortID()), name, siteName, (object)this, "Start"));
        }

        /// <summary>Starts the job.</summary>
        public virtual void Start()
        {
            this.PreProcessClonedChild(this.copyRoot);
            this.ProcessItem(this.sourceRoot, this.copyRoot);
        }

        /// <summary>Pre-process cloned child</summary>
        /// <param name="copy">The copy item.</param>
        protected virtual void PreProcessClonedChild(Item copy)
        {
            if (this.mapOriginalAndClonedItems == null)
                this.mapOriginalAndClonedItems = new Dictionary<ID, ID>();
            if (copy.OriginatorId != ID.Null)
            {
                if (!this.mapOriginalAndClonedItems.ContainsKey(copy.OriginatorId))
                    this.mapOriginalAndClonedItems.Add(copy.OriginatorId, copy.ID);
            }
            if (!this.deep)
                return;
            copy.GetChildren().ToList<Item>().ForEach(new Action<Item>(this.PreProcessClonedChild));
        }

        /// <summary>Processes item.</summary>
        /// <param name="source">The source item.</param>
        /// <param name="copy">The copy item.</param>
        protected virtual void ProcessItem(Item source, Item copy)
        {
            Assert.ArgumentNotNull((object)source, nameof(source));
            Assert.ArgumentNotNull((object)copy, nameof(copy));
            this.ProcessItemVersions(source, copy);
            if (!this.deep)
                return;
            foreach (Item child in copy.Children)
            {
                if (child.SourceUri != (ItemUri)null)
                {
                    Item source1 = Database.GetItem(child.SourceUri);
                    if (source1 != null)
                        this.ProcessItem(source1, child);
                }
            }
        }

        /// <summary>Processes item versions only.</summary>
        /// <param name="source">The source item.</param>
        /// <param name="copy">The copy item.</param>
        protected virtual void ProcessItemVersions(Item source, Item copy)
        {
            Assert.ArgumentNotNull((object)source, nameof(source));
            Assert.ArgumentNotNull((object)copy, nameof(copy));
            foreach (IGrouping<Language, ItemLink> source1 in this.GetCopyLinks(copy).GroupBy<ItemLink, Language>((Func<ItemLink, Language>)(x => x.SourceItemLanguage)))
            {
                foreach (IGrouping<Sitecore.Data.Version, ItemLink> grouping in source1.GroupBy<ItemLink, Sitecore.Data.Version>((Func<ItemLink, Sitecore.Data.Version>)(x => x.SourceItemVersion)))
                {
                    Language key = source1.Key;
                    Item copyVersion = this.GetVersion(copy.ID, key, grouping) ?? copy;
                    Assert.IsNotNull((object)copyVersion, string.Format("LinkDatabase is out of sync, cannot find item: {0}:{1}, language: {2}, version: {3}", (object)this.database.Name, (object)copy.ID, (object)key.Name, (object)grouping.Key.Number));
                    this.ProcessItemVersion(source, copyVersion, (IEnumerable<ItemLink>)grouping);
                }
            }
        }

        /// <summary>Get links of copy item</summary>
        /// <param name="copy">The copy item</param>
        /// <returns>Links of copy item</returns>
        protected virtual IEnumerable<ItemLink> GetCopyLinks(Item copy)
        {
            Assert.ArgumentNotNull((object)copy, nameof(copy));
            IEnumerable<ItemLink> source = (IEnumerable<ItemLink>)copy.Links.GetAllLinks(true, true);
            if (this.fieldIDs.Length != 0)
                source = source.Where<ItemLink>((Func<ItemLink, bool>)(x => ((IEnumerable<ID>)this.fieldIDs).Contains<ID>(x.SourceFieldID)));
            return source.Where<ItemLink>(new Func<ItemLink, bool>(this.NeedsProcessing)).Where<ItemLink>((Func<ItemLink, bool>)(link => !(FieldTypeManager.GetField(copy.Fields[link.SourceFieldID]) is VersionLinkField)));
        }

        /// <summary>Processes item versions only.</summary>
        /// <param name="source">The source item.</param>
        /// <param name="copyVersion">The copy version.</param>
        /// <param name="links">The links.</param>
        protected virtual void ProcessItemVersion(
          Item source,
          Item copyVersion,
          IEnumerable<ItemLink> links)
        {
            Assert.ArgumentNotNull((object)source, nameof(source));
            Assert.ArgumentNotNull((object)copyVersion, nameof(copyVersion));
            Assert.ArgumentNotNull((object)links, nameof(links));
            copyVersion.Editing.BeginEdit();
            foreach (ItemLink link in links)
                this.ProcessLink(link, source, copyVersion);
            Log.Audit("Replace links: {0}".FormatWith((object)AuditFormatter.FormatItem(copyVersion)), (object)this);
            copyVersion.Editing.EndEdit();
        }

        private Item GetVersion(
          ID id,
          Language language,
          IGrouping<Sitecore.Data.Version, ItemLink> versionGroup)
        {
            Assert.ArgumentNotNull((object)id, nameof(id));
            Assert.ArgumentNotNull((object)language, nameof(language));
            Assert.ArgumentNotNull((object)versionGroup, nameof(versionGroup));
            if (string.IsNullOrEmpty(language.Name))
                return (Item)null;
            Sitecore.Data.Version key = versionGroup.Key;
            return key.Number < 1 ? this.database.GetItem(id, language) : this.database.GetItem(id, language, key);
        }

        /// <summary>
        /// Returns <c>true</c> if link needs processing; <c>false</c> otherwise.
        /// </summary>
        /// <param name="link">The link to check.</param>
        /// <returns><c>true</c> if link needs processing; <c>false</c> otherwise.</returns>
        protected virtual bool NeedsProcessing(ItemLink link)
        {
            Assert.ArgumentNotNull((object)link, nameof(link));
            if (link.SourceFieldID == FieldIDs.Source)
                return false;
            Item targetItem = link.GetTargetItem();
            return targetItem != null && (targetItem == this.sourceRoot || targetItem.Axes.IsDescendantOf(this.sourceRoot));
        }

        /// <summary>Processes link</summary>
        /// <param name="link">The link.</param>
        /// <param name="source">The source item.</param>
        /// <param name="copyVersion">The copy item.</param>
        protected virtual void ProcessLink(ItemLink link, Item source, Item copyVersion)
        {
            Assert.ArgumentNotNull((object)link, nameof(link));
            Assert.ArgumentNotNull((object)source, nameof(source));
            Assert.ArgumentNotNull((object)copyVersion, nameof(copyVersion));
            Item targetItem = link.GetTargetItem();
            Assert.IsNotNull((object)targetItem, "linkTarget");
            string str = this.copyRoot.Paths.FullPath + targetItem.Paths.FullPath.Substring(this.sourceRootLength);
            Item newLink = (Item)null;
            if (this.mapOriginalAndClonedItems.ContainsKey(targetItem.ID))
                newLink = this.database.GetItem(this.mapOriginalAndClonedItems[targetItem.ID]);
            if (newLink == null)
            {
                Log.Warn("Cannot find corresponding item for {0} with path {1} in {2} database".FormatWith((object)source.Paths.FullPath, (object)str, (object)this.database.Name), (object)this);
            }
            else
            {
                CustomField field = FieldTypeManager.GetField(copyVersion.Fields[link.SourceFieldID]);
                Assert.IsNotNull((object)field, "customField");
                field.Relink(link, newLink);
            }
        }
    }
}
