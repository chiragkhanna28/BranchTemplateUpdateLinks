using System;
using System.Linq;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.StringExtensions;
using UpdateLinks.Jobs;

namespace UpdateLinks.Events
{
    public class FixBrokenLinks
    {
        private readonly bool _async;
        private readonly string _fieldIds;
        public FixBrokenLinks(string async, string fieldIds)
        {
            this._async = string.Equals(async, "true", StringComparison.OrdinalIgnoreCase);
            this._fieldIds = fieldIds;
        }
        public void OnItemAdded(object sender, EventArgs args)
        {
            Assert.ArgumentNotNull(args, "args");

            var contentItem = Event.ExtractParameter(args, 0) as Item;
            Assert.IsNotNull(contentItem, "targetItem");

            var branchItem = contentItem.Branch;
            if (branchItem == null)
            {
                return;
            }

            var item = branchItem.InnerItem;
            Assert.IsTrue(item.Children.Count == 1, "branch item structure is corrupted: {0}".FormatWith(AuditFormatter.FormatItem(item)));

            var branch = item.Children[0];
            ID[] fields = this._fieldIds.Split(',').Select(x => new ID(x)).ToArray();

            var replacementJob = new ItemReferencesReplacementJob(branch, contentItem, true, fields);
            if (this._async)
            {
                replacementJob.StartAsync();
            }
            else
            {
                replacementJob.Start();
            }

        }
    }
}
