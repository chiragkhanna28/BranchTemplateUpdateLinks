using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Jobs;
using Sitecore.Links;
using Sitecore.StringExtensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UpdateLinks.Jobs
{
    public class ItemReferencesReplacementJob : ReferenceReplacementJob
    {
        private readonly Database database;
        private readonly int sourceRootLength;
        private readonly Item copyRoot;
        private readonly bool deep;
        private readonly Item sourceRoot;
        private readonly ID[] fieldIDs = Array.Empty<ID>();
        private Dictionary<ID, ID> mapOriginalAndClonedItems;

        public ItemReferencesReplacementJob(Item source, Item copy) : base(source, copy)
        {
        }

        public ItemReferencesReplacementJob(Database database, string sourceId, string copyId) : base(database, sourceId, copyId)
        {
        }

        public ItemReferencesReplacementJob(Database database, ID sourceId, ID copyId) : base(database, sourceId, copyId)
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

        public ItemReferencesReplacementJob(Item source, Item copy, bool deep, ID[] fieldIDs) : base(source, copy, deep, fieldIDs)
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

        public ItemReferencesReplacementJob(Database database, string sourceId, string copyId, bool deep, string[] fieldIDs) : base(database, sourceId, copyId, deep, fieldIDs)
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


        /// <summary>Pre-process cloned child</summary>
        /// <param name="copy">The copy item.</param>
        protected override void PreProcessClonedChild(Item copy)
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

        /// <summary>Processes link</summary>
        /// <param name="link">The link.</param>
        /// <param name="source">The source item.</param>
        /// <param name="copyVersion">The copy item.</param>
        protected override void ProcessLink(ItemLink link, Item source, Item copyVersion)
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
