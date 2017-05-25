﻿using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using Nop.Admin.Infrastructure.Cache;
using Nop.Admin.Models.Home;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Common;
using Nop.Services.Configuration;
using Nop.Web.Framework.Mvc.Rss;

namespace Nop.Admin.Components
{
    public class NopCommerceNewsViewComponent : ViewComponent
    {
        private readonly AdminAreaSettings _adminAreaSettings;
        private readonly IStoreContext _storeContext;
        private readonly IStaticCacheManager _cacheManager;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;

        public NopCommerceNewsViewComponent(IStoreContext storeContext,
            AdminAreaSettings adminAreaSettings,
            ISettingService settingService,
            IStaticCacheManager cacheManager,
            IWebHelper webHelper)
        {
            this._storeContext = storeContext;
            this._adminAreaSettings = adminAreaSettings;
            this._settingService = settingService;
            this._cacheManager = cacheManager;
            this._webHelper = webHelper;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            try
            {
                string feedUrl = string
                    .Format(
                        "http://www.nopCommerce.com/NewsRSS.aspx?Version={0}&Localhost={1}&HideAdvertisements={2}&StoreURL={3}",
                        NopVersion.CurrentVersion,
                        _webHelper.IsLocalRequest(Request),
                        _adminAreaSettings.HideAdvertisementsOnAdminArea,
                        _storeContext.CurrentStore.Url)
                    .ToLowerInvariant();

                var rssData = _cacheManager.Get(ModelCacheEventConsumer.OFFICIAL_NEWS_MODEL_KEY, () =>
                {
                    //specify timeout (5 secs)
                    var request = WebRequest.Create(feedUrl);
                    request.Timeout = 5000;
                    using (var response = request.GetResponse())
                    using (var reader = XmlReader.Create(response.GetResponseStream()))
                    {
                        return RssFeed.Load(reader);
                    }
                });

                var model = new NopCommerceNewsModel
                {
                    HideAdvertisements = _adminAreaSettings.HideAdvertisementsOnAdminArea
                };

                for (int i = 0; i < rssData.Items.Count; i++)
                {
                    var item = rssData.Items.ElementAt(i);
                    var newsItem = new NopCommerceNewsModel.NewsDetailsModel
                    {
                        Title = item.TitleText,
                        Summary = item.ContentText,
                        Url = item.Url.OriginalString,
                        PublishDate = item.PublishDate
                    };
                    model.Items.Add(newsItem);

                    //has new items?
                    if (i == 0)
                    {
                        var firstRequest = String.IsNullOrEmpty(_adminAreaSettings.LastNewsTitleAdminArea);
                        if (_adminAreaSettings.LastNewsTitleAdminArea != newsItem.Title)
                        {
                            _adminAreaSettings.LastNewsTitleAdminArea = newsItem.Title;
                            _settingService.SaveSetting(_adminAreaSettings);

                            if (!firstRequest)
                            {
                                //new item
                                model.HasNewItems = true;
                            }
                        }
                    }
                }
                return View(model);
            }
            catch
            {
                return Content("");
            }
        }
    }
}
