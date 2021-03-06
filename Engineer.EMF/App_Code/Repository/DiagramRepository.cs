﻿using Engineer.EMF.App_Code.Utils;
using Engineer.EMF.Models;
using Engineer.EMF.Utils;
using Engineer.EMF.Utils.Exceptions;
using Engineer.Model;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Objects;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Engineer.EMF
{
    public class DiagramRepository : BaseRepository
    {
        public List<UserStoryAttachment> ListAll(string userId)
        {
            var diagrams = new List<UserStoryAttachment>();
            try
            {
                db.UserStoryAttachments.Where(w=>w.state != AppConstants.DIAGRAM_STATUS_FINISIHED).ToList().ForEach(diagram =>
                {
                    if(diagram.UserStory.state != AppConstants.USERSTORY_STATUS_DELETED)
                    {
                        diagram.UserStory.AspNetUsers.ToList().ForEach(
                        user =>
                        {
                            if (user != null && user.Id == userId)
                                diagrams.Add(diagram);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return diagrams;
        }

        public AttachmentHistory FindByHistoryID(int historyId)
        {
            return db.AttachmentHistories.SingleOrDefault(w => w.Id == historyId);
        }

        public List<AttachmentHistory> FindDiagramHistory(UserStoryAttachment diagramObject)
        {
            return db.AttachmentHistories.Where(w => w.AttachId == diagramObject.attachId && w.UserStoryId == diagramObject.userStoryId).OrderBy(o=>o.Date).ToList();
        }

        public List<UserStoryAttachment> FindAll()
        {
            return db.UserStoryAttachments.Where(w => w.state != AppConstants.DIAGRAM_STATUS_FINISIHED).ToList();
        }

        public List<UserStoryAttachment> FindByUsers(string[] users)
        {
            var attachments = new List<UserStoryAttachment>();
            users.ToList().ForEach(userId =>
            {
                attachments.AddRange(ListAll(userId));
            });
            return attachments;
        }

        public List<DiagramSearchModel> FindByUsersAndStories(string[] users, string[] stories, string sprint, string diagramName)
        {
            // get by closed user stories
            var diagrams = new List<DiagramSearchModel>();
            var attachments = new Dictionary<string,List<UserStoryAttachment>>();
            // get by diagramName
            if (!string.IsNullOrEmpty(diagramName))
            {
                attachments.Add("diagrams", db.UserStoryAttachments.Where(w => w.state != AppConstants.DIAGRAM_STATUS_FINISIHED && w.Attachment.name.Contains(diagramName) &&
                w.state == AppConstants.DIAGRAM_STATUS_CLOSED
                // user stories or sprints is closed
                /*(w.UserStory.state == AppConstants.USERSTORY_STATUS_FINISIHED || w.UserStory.Sprints.Select(s => s.state == AppConstants.SPRINT_STATUS_CLOSED).Count() > 0)*/).ToList())
                ;

            }



            // get by closed US
            if (stories != null && stories.Length > 0 && !stories.Contains("0"))
            {
               
                var closedStories = db.UserStories.Where(w => /*w.state == AppConstants.USERSTORY_STATUS_FINISIHED
                && */stories.Contains(w.Id.ToString()));
                var attachmentsByStories = new List<UserStoryAttachment>();
                closedStories.ToList().ForEach(f =>
                {
                    attachmentsByStories.AddRange(f.UserStoryAttachments.Where(w => w.state != AppConstants.DIAGRAM_STATUS_FINISIHED));
                });
                attachments.Add("stories", attachmentsByStories);
            }

            // get by closed sprint
            if (!string.IsNullOrEmpty(sprint) && sprint != "0")
            {
                var searchBySprints = sprint.Split(',');
                var closedSprints = db.Sprints./*Where(w => w.state == AppConstants.SPRINT_STATUS_CLOSED).*/ToList();
                var attachmentBySprint = new List<UserStoryAttachment>();
                foreach (var closedSprint in closedSprints)
                {
                    closedSprint.UserStories.ToList().ForEach(f =>
                    {
                        if (searchBySprints.Contains(closedSprint.Id.ToString()))
                        {
                            attachmentBySprint.AddRange(f.UserStoryAttachments.Where(
                            w =>
                            w.state != AppConstants.DIAGRAM_STATUS_FINISIHED).ToList());
                        }
                    });
                }
                attachments.Add("sprints", attachmentBySprint);
            }

            //get by users
            if (users != null && users.Length > 0)
            {
                var attachmentsByUsers = db.UserStoryAttachments.Where(w => (w.state == AppConstants.DIAGRAM_STATUS_CLOSED)
                && w.UserStory.AspNetUsers.Where(t => users.Contains(t.Id)).Count() > 0);
                attachments.Add("users", attachmentsByUsers.ToList());
            }

            // check if any of results not returned value, then empty result
            if (attachments.Values.Where(w => w.Count() == 0).Count() > 0) return new List<DiagramSearchModel>();
            else
            {
                List<UserStoryAttachment> sharedStories = new List<UserStoryAttachment>();
                attachments.Values.ToList().ForEach(result =>
                {
                    if (sharedStories.Count() == 0)
                        sharedStories = result;
                    else
                        sharedStories = sharedStories.Intersect(result, new SearchComparer()).ToList(); 
                });
                sharedStories.ToList().ForEach(d =>
                {
                    int sprintNumber = !string.IsNullOrEmpty(sprint) && sprint != "0" ? int.Parse(sprint) : 0;
                    var closedHistory = d.Attachment.AttachmentHistories.Where(w => w.UserStoryId == d.userStoryId && d.attachId == w.AttachId && w.state == AppConstants.DIAGRAM_STATUS_CLOSED).ToList();
                    closedHistory.ForEach(history =>
                    {
                        diagrams.Add(new DiagramSearchModel()
                        {
                            HistoryId = history.Id,
                            AttachmentId = d.attachId,
                            DiagramName = d.Attachment.name,
                            SprintNumber = !string.IsNullOrEmpty(sprint) && sprint != "0" ? Convert.ToString(db.Sprints.SingleOrDefault(s => s.Id == sprintNumber).number) :
                         (d.UserStory.Sprints != null ? String.Join(",", d.UserStory.Sprints.Select(s => s.number)) : ""),
                            UserStoryName = d.UserStory.name,
                            UserStoryId = d.UserStory.Id,
                            Users = db.AspNetUsers.SingleOrDefault(w=>w.Id == history.AspNetUser.Id).UserName,
                            Date = Convert.ToDateTime(history.Date),
                            Version = Convert.ToInt32(history.version),
                            CreatedBy = d.Attachment.AspNetUser.UserName
                        });
                    });
                    
                });
            }
               
                
            diagrams = diagrams.Where(w => w.UserStoryId > 0 ).Distinct(new AttachUSAndSprintComparer()).ToList();
            return diagrams;
            
        }

        public List<UserStoryAttachment> FindByStoryID(int id)
        {
            return db.UserStoryAttachments.Where(w => w.UserStory.Id == id).ToList();
        }

        

        public int Add(Attachment diagram, string userId)
        {
            diagram.created_by = userId;
            diagram.create_date = DateTime.Now;
            diagram.AttachmentHistories = null;
            Attachment attach = db.Attachments.Add(diagram);
            db.SaveChanges();
            return attach.Id;
        }

        public List<UserStoryAttachment> Update(UserStoryAttachment diagram,string userId)
        {
            //var exist = Get(diagram.attachId,diagram.userStoryId);
            var openDiagrams = GetAllOpenById(diagram.attachId);
            if(openDiagrams != null)
            {
                openDiagrams.ForEach(exist =>
                {
                    exist.activties = diagram.activties;
                    exist.update_date = DateTime.Now;
                    exist.update_by = userId;
                    exist.state = diagram.state;
                    exist.SVG = diagram.SVG;
                });
            }
            db.SaveChanges();
            return openDiagrams;
        }

        private List<UserStoryAttachment> GetAllOpenById(int diagramId)
        {
            return db.UserStoryAttachments.Where(w => w.attachId == diagramId && w.state != AppConstants.DIAGRAM_STATUS_FINISIHED && w.state != AppConstants.DIAGRAM_STATUS_CLOSED
           ).Where(locked => locked.@readonly == null || locked.@readonly == false).ToList();
        }

        public bool UpdateLock(UserStoryAttachment diagram,string userId,bool lockFlag)
        {
            try
            {
                if (diagram.state == AppConstants.DIAGRAM_STATUS_CLOSED && lockFlag)
                    return false;

                UserStoryAttachmentRepository rep = new UserStoryAttachmentRepository();
                var exist = rep.Get(diagram);
                if (exist.state != AppConstants.DIAGRAM_STATUS_FINISIHED)
                {
                    exist.@readonly = diagram.@readonly;
                    db.SaveChanges();
                    return true;
                }
                return false;
            }
            catch (NotExistItemException ex)
            {
                throw new BadRequestException(ex.Message);
            }
        }

        public UserStoryAttachment Get(int diagramId,int userStoryId)
        {
            var exist = db.UserStoryAttachments.SingleOrDefault(w => w.attachId == diagramId && w.state != AppConstants.DIAGRAM_STATUS_FINISIHED && w.userStoryId == userStoryId);
            if (exist == null)
                throw new NotExistItemException("Diagram id: " + diagramId + " Not exist");
            return exist;
        }

        public UserStoryAttachment FindByIDAndUserStory(Attachment attachment , UserStory us)
        {
            return db.UserStoryAttachments.SingleOrDefault(w => w.attachId == attachment.Id && w.userStoryId == us.Id);
        }

        public void SaveToHistory(UserStoryAttachment diagram, string userId)
        {
            AttachmentHistory history = new AttachmentHistory()
            {
                Graph = diagram.SVG,
                UserId = userId,
                Date = DateTime.Now,
                AttachId = diagram.attachId,
                UserStoryId = diagram.userStoryId,
                state = diagram.state,
                version = diagram.version
            };
            db.AttachmentHistories.Add(history);
            db.SaveChanges();
        }

        public List<UserStoryAttachment> FindBySprintID(int sprintId)
        {
            SprintRepository sRepository = new SprintRepository();
            var sprint = sRepository.Get(new Sprint() { Id = sprintId });
            var attachments = new List<UserStoryAttachment>();
            if (sprint.UserStories.Count() > 0)
            {
                sprint.UserStories.ToList().ForEach(us =>
                {
                    attachments.AddRange(us.UserStoryAttachments.Except(attachments, new AttachmentAndUserStoryComparer()));
                });
            }
            return attachments;
        }

    }
}
