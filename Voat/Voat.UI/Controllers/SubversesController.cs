﻿/*
This source file is subject to version 3 of the GPL license,
that is bundled with this package in the file LICENSE, and is
available online at http://www.gnu.org/licenses/gpl.txt;
you may not use this file except in compliance with the License.

Software distributed under the License is distributed on an "AS IS" basis,
WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License for
the specific language governing rights and limitations under the License.

All portions of the code written by Voat are Copyright (c) 2015 Voat, Inc.
All Rights Reserved.
*/

using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.Ajax.Utilities;
using Voat.Models;
using Voat.Models.ViewModels;

using System.Collections.Generic;
using Voat.Data.Models;
using Voat.Utilities;
using Voat.UI.Utilities;
using Voat.Configuration;
using Voat.Caching;
using Voat.Data;
using Voat.Domain.Query;
using Voat.Domain.Command;

namespace Voat.Controllers
{
    public class SubversesController : BaseController
    {
        //IAmAGate: Move queries to read-only mirror
        private readonly voatEntities _db = new voatEntities(true);

        private int subverseCacheTimeInSeconds = 240;

        // GET: sidebar for selected subverse
        public ActionResult SidebarForSelectedSubverseComments(Domain.Models.Submission submission)
        {
            //Can't cache as view is using model to query
            //var subverse = _db.Subverses.Find(submission.Subverse);
            var subverse = DataCache.Subverse.Retrieve(submission.Subverse);

            //don't return a sidebar since subverse doesn't exist or is a system subverse
            if (subverse == null)
            {
                return new EmptyResult();
            }
            
            try
            {
                ViewBag.OnlineUsers = SessionHelper.ActiveSessionsForSubverse(submission.Subverse);
            }
            catch (Exception)
            {
                ViewBag.OnlineUsers = -1;
            }

            ViewBag.Submission = submission;
            return PartialView("~/Views/Shared/Sidebars/_SidebarComments.cshtml", subverse);
        }

        // GET: sidebar for selected subverse
        public ActionResult SidebarForSelectedSubverse(string selectedSubverse)
        {
            //Can't cache as view is using Model to query
            var subverse = _db.Subverses.Find(selectedSubverse);

            // don't return a sidebar since subverse doesn't exist or is a system subverse
            if (subverse == null)
            {
                return new EmptyResult();
            }
            
            ViewBag.SelectedSubverse = selectedSubverse;

            try
            {
                ViewBag.OnlineUsers = SessionHelper.ActiveSessionsForSubverse(selectedSubverse);
            }
            catch (Exception)
            {
                ViewBag.OnlineUsers = -1;
            }

            return PartialView("~/Views/Shared/Sidebars/_Sidebar.cshtml", subverse);
        }

        // POST: Create a new Subverse
        [HttpPost]
        [Authorize]
        [VoatValidateAntiForgeryToken]
        [PreventSpam(DelayRequest = 300, ErrorMessage = "Sorry, you are doing that too fast. Please try again later.")]
        public async Task<ActionResult> CreateSubverse([Bind(Include = "Name, Title, Description, Type, Sidebar, CreationDate, Owner")] AddSubverse subverseTmpModel)
        {
            // abort if model state is invalid
            if (!ModelState.IsValid)
            {
                PreventSpamAttribute.Reset();
                return View(subverseTmpModel);
            }

            var title = $"/v/{subverseTmpModel.Name}"; //backwards compatibility, previous code always uses this
            var cmd = new CreateSubverseCommand(subverseTmpModel.Name, title, subverseTmpModel.Description, subverseTmpModel.Sidebar);
            var respones = await cmd.Execute();
            if (respones.Success)
            {
                return RedirectToAction("SubverseIndex", "Subverses", new { subverse = subverseTmpModel.Name });
            }
            else
            {
                ModelState.AddModelError(string.Empty, respones.Message);
                return View(subverseTmpModel);
            }
            
        }

        // GET: create
        [Authorize]
        public ActionResult CreateSubverse()
        {
            return View();
        }

       

        // GET: show a list of subverses by number of subscribers
        public ActionResult Subverses(int? page)
        {
            ViewBag.SelectedSubverse = "subverses";
            const int pageSize = 25;
            int pageNumber = (page ?? 0);

            if (pageNumber < 0)
            {
                return NotFoundErrorView();
            }

            try
            {
                // order by subscriber count (popularity)
                var subverses = _db.Subverses.OrderByDescending(s => s.SubscriberCount);

                var paginatedSubverses = new PaginatedList<Subverse>(subverses, page ?? 0, pageSize);

                return View(paginatedSubverses);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        // GET: show subverse search view
        public ActionResult Search()
        {
            ViewBag.SelectedSubverse = "subverses";
            ViewBag.SubversesView = "search";

            return View("~/Views/Subverses/SearchForSubverse.cshtml", new SearchSubverseViewModel());
        }

        [Authorize]
        public ViewResult SubversesSubscribed(int? page)
        {
            ViewBag.SelectedSubverse = "subverses";
            ViewBag.SubversesView = "subscribed";
            const int pageSize = 25;
            int pageNumber = (page ?? 0);

            if (pageNumber < 0)
            {
                return NotFoundErrorView();
            }

            // get a list of subcribed subverses with details and order by subverse names, ascending
            IQueryable<SubverseDetailsViewModel> subscribedSubverses = from c in _db.Subverses
                                                                       join a in _db.SubverseSubscriptions
                                                                       on c.Name equals a.Subverse
                                                                       where a.UserName.Equals(User.Identity.Name)
                                                                       orderby a.Subverse ascending
                                                                       select new SubverseDetailsViewModel
                                                                       {
                                                                           Name = c.Name,
                                                                           Title = c.Title,
                                                                           Description = c.Description,
                                                                           CreationDate = c.CreationDate,
                                                                           Subscribers = c.SubscriberCount
                                                                       };

            var paginatedSubscribedSubverses = new PaginatedList<SubverseDetailsViewModel>(subscribedSubverses, page ?? 0, pageSize);

            return View("SubscribedSubverses", paginatedSubscribedSubverses);
        }

        // GET: sidebar for selected subverse
        public ActionResult DetailsForSelectedSubverse(string selectedSubverse)
        {
            var subverse = DataCache.Subverse.Retrieve(selectedSubverse);

            if (subverse == null)
                return new EmptyResult();

            // get subscriber count for selected subverse
            //var subscriberCount = _db.SubverseSubscriptions.Count(r => r.Subverse.Equals(selectedSubverse, StringComparison.OrdinalIgnoreCase));

            //ViewBag.SubscriberCount = subscriberCount;
            ViewBag.SelectedSubverse = selectedSubverse;
            return PartialView("_SubverseDetails", subverse);

            //don't return a sidebar since subverse doesn't exist or is a system subverse
        }

        // GET: show a list of subverses by creation date
        public ViewResult NewestSubverses(int? page, string sortingmode)
        {
            ViewBag.SelectedSubverse = "subverses";
            ViewBag.SortingMode = sortingmode;

            const int pageSize = 25;
            int pageNumber = (page ?? 0);

            if (pageNumber < 0)
            {
                return NotFoundErrorView();
            }

            var subverses = _db.Subverses.Where(s => s.Description != null).OrderByDescending(s => s.CreationDate);

            var paginatedNewestSubverses = new PaginatedList<Subverse>(subverses, page ?? 0, pageSize);

            return View("~/Views/Subverses/Subverses.cshtml", paginatedNewestSubverses);
        }

        // show subverses ordered by last received submission
        public ViewResult ActiveSubverses(int? page)
        {
            ViewBag.SelectedSubverse = "subverses";
            ViewBag.SortingMode = "active";

            const int pageSize = 100;
            int pageNumber = (page ?? 0);

            if (pageNumber < 0)
            {
                return NotFoundErrorView();
            }
            var subverses = CacheHandler.Instance.Register("Legacy:ActiveSubverses", new Func<IList<Subverse>>(() => {
                using (var db = new voatEntities())
                {
                    db.EnableCacheableOutput();

                    //HACK: I'm either completely <censored> or this is a huge pain in EF (sorting on a joined column and using .Distinct()), what you see below is a total hack that 'kinda' works
                    return (from subverse in db.Subverses
                            join submission in db.Submissions on subverse.Name equals submission.Subverse
                            where subverse.Description != null && subverse.SideBar != null
                            orderby submission.CreationDate descending
                            select subverse).Take(pageSize).ToList().Distinct().ToList();
                }
            }), TimeSpan.FromMinutes(15));

            //Turn off paging and only show the top ~50 most active
            var paginatedActiveSubverses = new PaginatedList<Subverse>(subverses, 0, pageSize, pageSize);

            return View("~/Views/Subverses/Subverses.cshtml", paginatedActiveSubverses);
        }

        public ActionResult Subversenotfound()
        {
            ViewBag.SelectedSubverse = "404";
            return SubverseNotFoundErrorView();
        }

        public ActionResult AdultContentFiltered(string destination)
        {
            ViewBag.SelectedSubverse = destination;
            return View("~/Views/Subverses/AdultContentFiltered.cshtml");
        }

        public ActionResult AdultContentWarning(string destination, bool? nsfwok)
        {
            ViewBag.SelectedSubverse = String.Empty;

            if (destination == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            if (nsfwok != null && nsfwok == true)
            {
                // setup nswf cookie
                HttpCookie hc = new HttpCookie("NSFWEnabled", "1");
                hc.Expires = Repository.CurrentDate.AddYears(1);
                System.Web.HttpContext.Current.Response.Cookies.Add(hc);

                // redirect to destination subverse
                return RedirectToAction("SubverseIndex", "Subverses", new { subverse = destination });
            }
            ViewBag.Destination = destination;
            return View("~/Views/Subverses/AdultContentWarning.cshtml");
        }

        // GET: fetch a random subbverse with x subscribers and x submissions
        public ActionResult Random()
        {
            try
            {
                string randomSubverse = RandomSubverse(true);
                return RedirectToAction("SubverseIndex", "Subverses", new { subverse = randomSubverse });
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        // GET: fetch a random NSFW subbverse with x subscribers and x submissions
        public ActionResult RandomNsfw()
        {
            try
            {
                string randomSubverse = RandomSubverse(false);
                return RedirectToAction("SubverseIndex", "Subverses", new { subverse = randomSubverse });
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        public async Task<ContentResult> Stylesheet(string subverse, bool cache = true, bool minimized = true)
        {
            var policy = (cache ? new CachePolicy(TimeSpan.FromMinutes(30)) : CachePolicy.None);
            var q = new QuerySubverseStylesheet(subverse, policy);

            var madStylesYo = await q.ExecuteAsync();

            return new ContentResult()
            {
                Content = (minimized ? madStylesYo.Minimized : madStylesYo.Raw),
                ContentType = "text/css"
            };
        }



        // GET: render a partial view with list of moderators for a given subverse, if no moderators are found, return subverse owner
        [ChildActionOnly]
        public ActionResult SubverseModeratorsList(string subverseName)
        {
            var q = new QuerySubverseModerators(subverseName);
            var r = q.Execute();
            return PartialView("~/Views/Subverses/_SubverseModerators.cshtml", r);
        }

        // GET: stickied submission
        [ChildActionOnly]
        public ActionResult StickiedSubmission(string subverseName)
        {
            var stickiedSubmission = StickyHelper.GetSticky(subverseName);

            if (stickiedSubmission != null)
            {
                return PartialView("_Stickied", stickiedSubmission);
            }
            else
            {
                return new EmptyResult();
            }
        }

        // GET: list of default subverses
        public ActionResult ListOfDefaultSubverses()
        {
            try
            {
                var q = new QueryDefaultSubverses();
                var r = q.Execute();

                //var listOfSubverses = _db.DefaultSubverses.OrderBy(s => s.Order).ToList();
                return PartialView("_ListOfDefaultSubverses", r);
            }
            catch (Exception)
            {
                return new EmptyResult();
            }
        }

        [Authorize]

        // GET: list of subverses user is subscribed to, used in hover menu
        public ActionResult ListOfSubversesUserIsSubscribedTo()
        {
            // show custom list of subverses in top menu
            var listOfSubverses = _db.SubverseSubscriptions
                .Where(s => s.UserName == User.Identity.Name)
                .OrderBy(s => s.Subverse);

            return PartialView("_ListOfSubscribedToSubverses", listOfSubverses);
        }

        // POST: subscribe to a subverse
        [Authorize]
        public async Task<JsonResult> Subscribe(string subverseName)
        {
            var cmd = new SubscriptionCommand(new Domain.Models.DomainReference(Domain.Models.DomainType.Subverse, subverseName), Domain.Models.SubscriptionAction.Subscribe);
            var r = await cmd.Execute();
            if (r.Success)
            {
                return Json("Subscription request was successful.", JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json(r.Message, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: unsubscribe from a subverse
        [Authorize]
        public async Task<JsonResult> UnSubscribe(string subverseName)
        {
            //var loggedInUser = User.Identity.Name;

            //Voat.Utilities.UserHelper.UnSubscribeFromSubverse(loggedInUser, subverseName);
            //return Json("Unsubscribe request was successful.", JsonRequestBehavior.AllowGet);
            var cmd = new SubscriptionCommand(new Domain.Models.DomainReference(Domain.Models.DomainType.Subverse, subverseName), Domain.Models.SubscriptionAction.Unsubscribe);
            var r = await cmd.Execute();
            if (r.Success)
            {
                return Json("Unsubscribe request was successful.", JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json(r.Message, JsonRequestBehavior.AllowGet);
            }

        }
        
        // POST: block a subverse
        [Authorize]
        public async Task<JsonResult> BlockSubverse(string subverseName)
        {
            var loggedInUser = User.Identity.Name;
            var cmd = new BlockCommand(Domain.Models.DomainType.Subverse, subverseName);
            var response = await cmd.Execute();

            if (response.Success)
            {
                return Json("Subverse block request was successful.", JsonRequestBehavior.AllowGet);
            }
            else
            {
                Response.StatusCode = 400;
                return Json(response.Message, JsonRequestBehavior.AllowGet);
            }
        }

        #region Submission Display Methods

        private void RecordSession(string subverse)
        {
            //TODO: Relocate this to a command object
            // register a new session for this subverse
            try
            {
                // register a new session for this subverse
                string clientIpAddress = UserHelper.UserIpAddress(Request);
                string ipHash = IpHash.CreateHash(clientIpAddress);
                SessionHelper.Add(subverse, ipHash);

                ViewBag.OnlineUsers = SessionHelper.ActiveSessionsForSubverse(subverse);
            }
            catch (Exception)
            {
                ViewBag.OnlineUsers = -1;
            }
        }
        private void SetFirstTimeCookie()
        {
            // setup a cookie to find first time visitors and display welcome banner
            const string cookieName = "NotFirstTime";
            if (ControllerContext.HttpContext.Request.Cookies.AllKeys.Contains(cookieName))
            {
                // not a first time visitor
                ViewBag.FirstTimeVisitor = false;
            }
            else
            {
                // add a cookie for first time visitors
                HttpCookie hc = new HttpCookie("NotFirstTime", "1");
                hc.Expires = Repository.CurrentDate.AddYears(1);
                System.Web.HttpContext.Current.Response.Cookies.Add(hc);

                ViewBag.FirstTimeVisitor = true;
            }
        }

        // GET: show a subverse index
        public async Task<ActionResult> SubverseIndex(int? page, string subverse, string sort = "hot", string time = "day", bool? previewMode = null)
        {
            const string cookieName = "NSFWEnabled";
            int pageNumber = (page ?? 0);

            if (pageNumber < 0)
            {
                return NotFoundErrorView();
            }
            var viewProperties = new SubmissionListViewModel();
            viewProperties.PreviewMode = previewMode ?? false;

            //Set to DEFAULT if querystring is present
            if (Request.QueryString["frontpage"] == "guest")
            {
                subverse = AGGREGATE_SUBVERSE.DEFAULT;
            }
            if (String.IsNullOrEmpty(subverse))
            {
                return SubverseNotFoundErrorView();
            }

            SetFirstTimeCookie();
            RecordSession(subverse);

            var options = new SearchOptions();
            options.Page = pageNumber;
            options.Count = 25;


            var sortAlg = Domain.Models.SortAlgorithm.New;
            if (!Enum.TryParse(sort, true, out sortAlg))
            {
                throw new NotImplementedException("sort " + sort + " is unknown");
            }
            options.Sort = sortAlg;
            //Set Top data
            if (sortAlg == Domain.Models.SortAlgorithm.Top)
            {
                //set defaults
                if (String.IsNullOrEmpty(time))
                {
                    time = "day";
                }
                Domain.Models.SortSpan span = Domain.Models.SortSpan.Day;
                if (!Enum.TryParse(time, true, out span))
                {
                    throw new NotImplementedException("span " + time + " is unknown");
                }
                
                options.Span = span;
            }

            //Null out defaults
            viewProperties.Sort = options.Sort == Domain.Models.SortAlgorithm.Rank ? (Domain.Models.SortAlgorithm?)null : options.Sort;
            viewProperties.Span = options.Span == Domain.Models.SortSpan.All ? (Domain.Models.SortSpan?)null : options.Span;
           
            try
            {
                PaginatedList<Domain.Models.Submission> pageList = null;

                if (AGGREGATE_SUBVERSE.IsAggregate(subverse))
                {
                    if (AGGREGATE_SUBVERSE.FRONT.IsEqual(subverse))
                    {
                        //Check if user is logged in and has subscriptions, if not we convert to default query
                        if (!User.Identity.IsAuthenticated || (User.Identity.IsAuthenticated && !UserData.HasSubscriptions()))
                        {
                            subverse = AGGREGATE_SUBVERSE.DEFAULT;
                        }
                        //viewProperties.Title = "Front";
                        //ViewBag.SelectedSubverse = "frontpage";
                    }
                    else if (AGGREGATE_SUBVERSE.DEFAULT.IsEqual(subverse))
                    {
                        //viewProperties.Title = "Front";
                        //ViewBag.SelectedSubverse = "frontpage";
                    }
                    else
                    {
                        // selected subverse is ALL, show submissions from all subverses, sorted by rank
                        viewProperties.Title = "all subverses";
                        viewProperties.Subverse = "all";
                        subverse = AGGREGATE_SUBVERSE.ALL;
                        //ViewBag.SelectedSubverse = "all";
                        //ViewBag.Title = "all subverses";
                    }
                }
                else
                {
                    // check if subverse exists, if not, send to a page not found error
                    //Can't use cached, view using to query db
                    var subverseObject = _db.Subverses.Find(subverse);

                    if (subverseObject == null)
                    {
                        ViewBag.SelectedSubverse = "404";
                        return SubverseNotFoundErrorView();
                    }

                    //HACK: Disable subverse
                    if (subverseObject.IsAdminDisabled.HasValue && subverseObject.IsAdminDisabled.Value)
                    {
                        //viewProperties.Subverse = subverseObject.Name;
                        ViewBag.Subverse = subverseObject.Name;
                        return SubverseDisabledErrorView();
                    }

                    //Check NSFW Settings
                    if (subverseObject.IsAdult)
                    {
                        if (User.Identity.IsAuthenticated)
                        {
                            if (!UserData.Preferences.EnableAdultContent)
                            {
                                // display a view explaining that account preference is set to NO NSFW and why this subverse can not be shown
                                return RedirectToAction("AdultContentFiltered", "Subverses", new { destination = subverseObject.Name });
                            }
                        }
                        // check if user wants to see NSFW content by reading NSFW cookie
                        else if (!ControllerContext.HttpContext.Request.Cookies.AllKeys.Contains(cookieName))
                        {
                            return RedirectToAction("AdultContentWarning", "Subverses", new { destination = subverseObject.Name, nsfwok = false });
                        }
                    }

                    viewProperties.Subverse = subverseObject.Name;
                    viewProperties.Title = subverseObject.Description;
                }


                var q = new QuerySubmissions(subverse, Domain.Models.DomainType.Subverse, options);
                var results = await q.ExecuteAsync().ConfigureAwait(false);

                pageList = new PaginatedList<Domain.Models.Submission>(results, options.Page, options.Count, -1);
                viewProperties.Submissions = pageList;
                viewProperties.Subverse = subverse;

                //Backwards compat with Views
                if (subverse == AGGREGATE_SUBVERSE.FRONT || subverse == AGGREGATE_SUBVERSE.DEFAULT)
                {
                    ViewBag.SelectedSubverse = "frontpage";
                }
                else if (subverse == AGGREGATE_SUBVERSE.ALL || subverse == AGGREGATE_SUBVERSE.ANY)
                {
                    ViewBag.SelectedSubverse = "all";
                }
                else 
                {
                    ViewBag.SelectedSubverse = subverse;
                }
                ViewBag.SortingMode = sort;
                
                return View(viewProperties);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        //TODO: Move to dedicated query object
        //[Obsolete("Arg Matie, you shipwrecked upon t'is Dead Code", true)]
        private IQueryable<Submission> SfwSubmissionsFromAllSubversesByViews24Hours(voatEntities _db)
        {
            if (_db == null)
            {
                _db = this._db;
            }
            var startDate = Repository.CurrentDate.Add(new TimeSpan(0, -24, 0, 0, 0));
            IQueryable<Submission> sfwSubmissionsFromAllSubversesByViews24Hours = (from message in _db.Submissions
                                                                                   join subverse in _db.Subverses on message.Subverse equals subverse.Name
                                                                                   where message.ArchiveDate == null && !message.IsDeleted && subverse.IsPrivate != true && subverse.IsAdminPrivate != true && subverse.IsAdult == false && message.CreationDate >= startDate && message.CreationDate <= Repository.CurrentDate
                                                                                   where !(from bu in _db.BannedUsers select bu.UserName).Contains(message.UserName)
                                                                                   where !subverse.IsAdminDisabled.Value
                                                                                   where !(from ubs in _db.UserBlockedSubverses where ubs.Subverse.Equals(subverse.Name) select ubs.UserName).Contains(User.Identity.Name)
                                                                                   select message).OrderByDescending(s => s.Views).DistinctBy(m => m.Subverse).Take(5).AsQueryable().AsNoTracking();

            return sfwSubmissionsFromAllSubversesByViews24Hours;
        }

        #endregion



        [ChildActionOnly]
        [OutputCache(Duration = 600, VaryByParam = "none")]
        public ActionResult TopViewedSubmissions24Hours()
        {
            //var submissions =
            var cacheData = CacheHandler.Instance.Register("legacy:TopViewedSubmissions24Hours", new Func<object>(() =>
            {
                using (voatEntities db = new voatEntities(CONSTANTS.CONNECTION_READONLY))
                {
                    db.EnableCacheableOutput();

                    return SfwSubmissionsFromAllSubversesByViews24Hours(db).ToList();
                }
            }), TimeSpan.FromMinutes(60), 5);

            return PartialView("_MostViewedSubmissions", cacheData);
        }

        #region random subverse

        public string RandomSubverse(bool sfw)
        {
            // fetch a random subverse with minimum number of subscribers where last subverse activity was evident
            IQueryable<Subverse> subverse;
            if (sfw)
            {
                subverse = from subverses in
                               _db.Subverses
                                   .Where(s => s.SubscriberCount > 10 && !s.Name.Equals("all", StringComparison.OrdinalIgnoreCase) && s.LastSubmissionDate != null
                                   && !(from ubs in _db.UserBlockedSubverses where ubs.Subverse.Equals(s.Name) select ubs.UserName).Contains(User.Identity.Name)
                                   && !s.IsAdult
                                   && !s.IsAdminDisabled.Value)
                           select subverses;
            }
            else
            {
                subverse = from subverses in
                               _db.Subverses
                                   .Where(s => s.SubscriberCount > 10 && !s.Name.Equals("all", StringComparison.OrdinalIgnoreCase)
                                               && s.LastSubmissionDate != null
                                               && !(from ubs in _db.UserBlockedSubverses where ubs.Subverse.Equals(s.Name) select ubs.UserName).Contains(User.Identity.Name)
                                               && s.IsAdult
                                               && !s.IsAdminDisabled.Value)
                           select subverses;
            }

            var submissionCount = 0;
            Subverse randomSubverse;

            do
            {
                var count = subverse.Count(); // 1st round-trip
                var index = new Random().Next(count);

                randomSubverse = subverse.OrderBy(s => s.Name).Skip(index).FirstOrDefault(); // 2nd round-trip

                var submissions = _db.Submissions
                        .Where(x => x.Subverse == randomSubverse.Name && !x.IsDeleted)
                        .OrderByDescending(s => s.Rank)
                        .Take(50)
                        .ToList();

                if (submissions.Count > 9)
                {
                    submissionCount = submissions.Count;
                }
            } while (submissionCount == 0);

            return randomSubverse != null ? randomSubverse.Name : "all";
        }

        #endregion random subverse
    }
}
