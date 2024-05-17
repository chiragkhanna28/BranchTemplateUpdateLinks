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
    public class UpdateLinks
    {
        private readonly bool async;
        private readonly string fieldIds;
        public UpdateLinks()
        {
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
            ID[] fields = fieldIds.Split(',').Select(x => new ID(x)).ToArray();

            var replacementJob = new ItemReferencesReplacementJob(branch, contentItem, true, fields);
            replacementJob.Start();
        }
    }
}
