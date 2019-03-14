﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace ProStudCreator
{
    public static class ProjectExtensions
    {
        #region Actions

        public static void InitNew(this Project _p)
        {
            _p.Creator = ShibUser.GetEmail();
            _p.CreateDate = DateTime.Now;
            _p.PublishedDate = DateTime.Now;
            _p.State = ProjectState.InProgress;
            _p.IsMainVersion = true;
        }

        public static string GetFullNr(this Project _p) => $"{_p.Semester.Name}_{_p.Department.DepartmentName}{_p.ProjectNr:D2}";

        public static string GetFullTitle(this Project _p) => $"{_p.Semester.Name}_{_p.Department.DepartmentName}{_p.ProjectNr:D2}: {_p.Name}";


        public static Project CopyProject(this Project _p)
        {
            Project copy = new Project();
            _p.MapProject(copy);
            copy.ModificationDate = _p.ModificationDate;
            copy.LastEditedBy = _p.LastEditedBy;
            copy.IsMainVersion = false;
            return copy;
        }

        public static void SaveProjectAsMainVersion(this Project _p, ProStudentCreatorDBDataContext db)
        {
            _p.ModificationDate = DateTime.Now;
            _p.LastEditedBy = ShibUser.GetEmail();

            foreach (Project proj in db.Projects.Where(p => p.BaseVersionId == _p.BaseVersionId && p.IsMainVersion == true))
            {
                proj.IsMainVersion = false;
            }

            _p.IsMainVersion = true;
            db.SubmitChanges();
        }

        public static Project CopyAndUseCopyAsMainVersion(this Project _p, ProStudentCreatorDBDataContext db)
        {
            Project copy = new Project();
            _p.MapProject(copy);
            copy.ModificationDate = DateTime.Now;
            copy.LastEditedBy = ShibUser.GetEmail();

            foreach (Project proj in db.Projects.Where(p => p.BaseVersionId == _p.BaseVersionId && p.IsMainVersion == true))
            {
                proj.IsMainVersion = false;
            }

            copy.IsMainVersion = true;

            db.Projects.InsertOnSubmit(copy);
            db.SubmitChanges();
            return copy;
        }

        public static Project DuplicateProject(this Project _p, ProStudentCreatorDBDataContext db)
        {
            var duplicate = _p.Duplicate(db);
            duplicate.BaseVersionId = duplicate.Id;
            duplicate.Reservation1Mail = "";
            duplicate.Reservation2Mail = "";
            duplicate.Reservation1Name = "";
            duplicate.Reservation2Name = "";
            duplicate.State = ProjectState.InProgress;
            duplicate.ClearLog(db);
            duplicate.IsMainVersion = true;
            duplicate.Name += " (Duplikat)";
            db.SubmitChanges();
            return duplicate;
        }

        public static void ClearLog(this Project _p, ProStudentCreatorDBDataContext db)
        {
            _p.LogDefenceDate = null;
            _p.LogDefenceRoom = null;
            _p.LogExpertID = null;
            _p.LogExpertPaid = false;
            _p.LogGradeStudent1 = null;
            _p.LogGradeStudent2 = null;
            _p.LogLanguageEnglish = null;
            _p.LogLanguageGerman = null;
            _p.LogProjectDuration = null;
            _p.LogProjectType = null;
            _p.LogProjectTypeID = null;
            _p.LogStudent1Mail = null;
            _p.LogStudent1Name = null;
            _p.LogStudent2Mail = null;
            _p.LogStudent2Name = null;
            db.SubmitChanges();
        }

        public static bool IsModified(this Project p1, Project p2)
        {

            //Folgende Eîgenschaftene werden ignoriert, da sie entweder vom Benutzer nicht geändert werden können 
            //  oder nur etwas enthalten, wenn das Objekt aus der Datanbank stammt (Relationen) 
            List<string> exclusionList = new List<string>();
            var projectType = typeof(Project);
            var pid = nameof(Project.Id);
            var modDate = nameof(Project.ModificationDate);
            var pubDate = nameof(Project.PublishedDate);
            var lastEditedBy = nameof(Project.LastEditedBy);
            var projectNr = nameof(Project.ProjectNr);
            var isMainVers = nameof(Project.IsMainVersion);
            var projId = nameof(Project.BaseVersionId);
            var credate = nameof(Project.CreateDate);
            var prs = nameof(Project.Projects);
            var attch = nameof(Project.Attachements);
            var tsk = nameof(Project.Tasks);
            var stateColor = nameof(Project.StateColor);
            var stateAsString = nameof(Project.StateAsString);
            var creator = nameof(Project.Creator);
            var semester = nameof(Project.Semester);
            var pOneType = nameof(Project.POneType);
            var pTwoType = nameof(Project.PTwoType);
            var pOneTypeTeamSize = nameof(Project.POneTeamSize);
            var pTwoTypeTeamSize = nameof(Project.PTwoTeamSize);
            var advisor1 = nameof(Project.Advisor1);
            var advisor2 = nameof(Project.Advisor2);

            exclusionList.Add(pid);
            exclusionList.Add(modDate);
            exclusionList.Add(pubDate);
            exclusionList.Add(lastEditedBy);
            exclusionList.Add(projId);
            exclusionList.Add(projectNr);
            exclusionList.Add(isMainVers);
            exclusionList.Add(credate);
            exclusionList.Add(prs);
            exclusionList.Add(attch);
            exclusionList.Add(tsk);
            exclusionList.Add(stateColor);
            exclusionList.Add(stateAsString);
            exclusionList.Add(creator);
            exclusionList.Add(semester);
            exclusionList.Add(pOneType);
            exclusionList.Add(pTwoType);
            exclusionList.Add(pOneTypeTeamSize);
            exclusionList.Add(pTwoTypeTeamSize);
            exclusionList.Add(advisor1);
            exclusionList.Add(advisor2);

            //don't check picture
            var pic = nameof(Project.Picture);
            exclusionList.Add(pic);

            foreach (PropertyInfo pi in typeof(Project).GetProperties())
            {
                if (!exclusionList.Contains(pi.Name))
                {

                    var value1 = pi.GetValue(p1);
                    var value2 = pi.GetValue(p2);
                    if (value1 != null && value2 != null)
                    {
                        if (!value1.Equals(value2))
                            return true;
                    }
                    else if (value1 != null && value2 == null)
                    {
                        return true;
                    }
                    else if (value1 == null && value2 != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static Project Duplicate(this Project _p, ProStudentCreatorDBDataContext db)
        {
            Project duplicatedProject = new Project
            {
                ModificationDate = DateTime.Now,
                LastEditedBy = ShibUser.GetEmail()
            };
            _p.MapProject(duplicatedProject);
            db.Projects.InsertOnSubmit(duplicatedProject);
            db.SubmitChanges();
            return duplicatedProject;
        }

        /// <summary>
        ///     Validates the user's input and generates an error message for invalid input.
        ///     One message is returned at a time, processed top to bottom.
        /// </summary>
        /// <returns>First applicable error message from the validation.</returns>
        public static string GenerateValidationMessage(this Project _p, bool[] projectTypes = null)
        {
            string validationMessage = "";
            if (_p.Advisor1 == null)
                validationMessage = "Bitte wählen Sie einen Hauptbetreuer aus.";

            if (_p.ClientPerson.Trim().Length != 0 && !_p.ClientPerson.IsValidName())
                validationMessage = "Bitte geben Sie den Namen des Kundenkontakts an (Vorname Nachname).";

            if (_p.ClientMail.Trim().Length != 0 && !_p.ClientMail.IsValidEmail())
                validationMessage = "Bitte geben Sie die E-Mail-Adresse des Kundenkontakts an.";

            /*
            if ((!_p.Advisor1?.Name.IsValidName()) ?? true)
                validationMessage = "Bitte wählen Sie einen Hauptbetreuer aus.";
            */

            var numAssignedTypes = 0;
            if (projectTypes == null)
            {
                projectTypes = _p.GetProjectTypeBools();
            }

            numAssignedTypes = projectTypes.Count(a => a);


            if (numAssignedTypes != 1 && numAssignedTypes != 2)
                validationMessage = "Bitte wählen Sie genau 1-2 passende Themengebiete aus.";

            if (_p.OverOnePage)
                validationMessage = "Der Projektbeschrieb passt nicht auf eine A4-Seite. Bitte kürzen Sie die Beschreibung.";

            if (!ShibUser.CanSubmitAllProjects() && !_p.UserHasAdvisor1Rights())
                validationMessage = "Nur Hauptbetreuer können Projekte einreichen.";

            if (_p.Reservation1Mail.Trim().Length != 0 && _p.Reservation1Name.Trim().Length == 0)
                validationMessage = "Bitte geben Sie den Namen der ersten Person an, für die das Projekt reserviert ist (Vorname Nachname).";

            if (_p.Reservation1Mail.Trim().Length != 0 && _p.Reservation1Name.Trim().Length != 0)
            {
                Regex regex = new Regex(@".*\..*@students\.fhnw\.ch");
                System.Text.RegularExpressions.Match match = regex.Match(_p.Reservation1Mail);
                if (!match.Success)
                    validationMessage = "Bitte geben Sie eine gültige E-Mail-Adresse der Person an, für die das Projekt reserviert ist. (vorname.nachname@students.fhnw.ch)";
            }

            if (_p.Reservation2Mail.Trim().Length != 0 && _p.Reservation2Name.Trim().Length == 0)
                validationMessage =
                    "Bitte geben Sie den Namen der zweiten Person an, für die das Projekt reserviert ist (Vorname Nachname).";

            if (_p.Reservation2Mail.Trim().Length != 0 && _p.Reservation2Name.Trim().Length != 0)
            {
                Regex regex = new Regex(@".*\..*@students\.fhnw\.ch");
                System.Text.RegularExpressions.Match match = regex.Match(_p.Reservation1Mail);
                match = regex.Match(_p.Reservation2Mail);
                if (!match.Success)
                    validationMessage = "Bitte geben Sie eine gültige E-Mail-Adresse der zweiten Person an, für die das Projekt reserviert ist.(vorname.nachname@students.fhnw.ch)";
            }

            if (_p.Reservation1Name.Trim().Length != 0 && _p.Reservation1Mail.Trim().Length == 0)
                validationMessage = "Bitte geben Sie die E-Mail-Adresse der Person an, für die das Projekt reserviert ist.";

            if (_p.Reservation2Name.Trim().Length != 0 && _p.Reservation2Mail.Trim().Length == 0)
                validationMessage = "Bitte geben Sie die E-Mail-Adresse der zweiten Person an, für die das Projekt reserviert ist.";

            return validationMessage;
        }

        private static bool[] GetProjectTypeBools(this Project _p)
        {
            bool[] projectType = new bool[8];
            if (_p.TypeDesignUX)
                projectType[0] = true;
            if (_p.TypeHW)
                projectType[1] = true;
            if (_p.TypeCGIP)
                projectType[2] = true;
            if (_p.TypeMlAlg)
                projectType[3] = true;
            if (_p.TypeAppWeb)
                projectType[4] = true;
            if (_p.TypeDBBigData)
                projectType[5] = true;
            if (_p.TypeSysSec)
                projectType[6] = true;
            if (_p.TypeSE)
                projectType[7] = true;
            return projectType;
        }


        /***
         * 
         * MapProject creates a duplicate of a project. This can't be done with reflection as it has some issues when fields are null.
         * Doing it this way, the problem is if you change the Project you will have to update this method or the field won't get copied.
         * This is currently solved with a constant which has to be manually updated after the method has been updated
         * 
         */
        public static void MapProject(this Project _p, Project target)
        {
            int EXPECTEDPROPCOUNT = 91; // has to be updated after the project class has changed and the method has been updated

            var actualPropCount = typeof(Project).GetProperties().Count();

            if (actualPropCount != EXPECTEDPROPCOUNT)
                throw new Exception("The Save-Method is outdated. You have mostlikely edited the DBML. Please update ProjectExtension.cs AND the constant 'EXPECTEDPROPCOUNT'. PropertyCount: " + actualPropCount);

            target.Ablehnungsgrund = _p.Ablehnungsgrund;
            target.Advisor1 = _p.Advisor1;
            target.Advisor2 = _p.Advisor2;
            target.Attachements = _p.Attachements;
            target.BaseVersionId = _p.BaseVersionId;
            target.BillingStatus = _p.BillingStatus;
            target.ClientAddressCity = _p.ClientAddressCity;
            target.ClientAddressDepartment = _p.ClientAddressDepartment;
            target.ClientAddressPostcode = _p.ClientAddressPostcode;
            target.ClientAddressStreet = _p.ClientAddressStreet;
            target.ClientAddressTitle = _p.ClientAddressTitle;
            target.ClientCompany = _p.ClientCompany;
            target.ClientMail = _p.ClientMail;
            target.ClientPerson = _p.ClientPerson;
            target.ClientReferenceNumber = _p.ClientReferenceNumber;
            target.ClientType = _p.ClientType;
            target.CreateDate = _p.CreateDate;
            target.Creator = _p.Creator;
            target.Department = _p.Department;
            target.DurationOneSemester = target.DurationOneSemester;
            target.Expert = _p.Expert;
            target.ImgDescription = _p.ImgDescription;
            target.Important = _p.Important;
            target.InitialPosition = _p.InitialPosition;
            //target.IsContinuation = _p.IsContinuation;
            //target.IsMainVersion = _p.IsMainVersion;
            target.LanguageEnglish = _p.LanguageEnglish;
            target.LanguageGerman = _p.LanguageGerman;
            target.LogDefenceDate = _p.LogDefenceDate;
            target.LogDefenceRoom = _p.LogDefenceRoom;
            target.LogExpertID = _p.LogExpertID;
            target.LogExpertPaid = _p.LogExpertPaid;
            target.LogGradeStudent1 = _p.LogGradeStudent1;
            target.LogGradeStudent2 = _p.LogGradeStudent2;
            target.LogLanguageEnglish = _p.LogLanguageEnglish;
            target.LogLanguageGerman = _p.LogLanguageGerman;
            target.LogProjectDuration = _p.LogProjectDuration;
            target.LogProjectType = _p.LogProjectType;
            target.LogProjectTypeID = _p.LogProjectTypeID;
            target.LogStudent1Mail = _p.LogStudent1Mail;
            target.LogStudent1Name = _p.LogStudent1Name;
            target.LogStudent2Mail = _p.LogStudent2Mail;
            target.LogStudent2Name = _p.LogStudent2Name;
            target.Name = _p.Name;
            target.Objective = _p.Objective;
            target.OverOnePage = _p.OverOnePage;
            target.Picture = _p.Picture;
            target.POneTeamSize = _p.POneTeamSize;
            target.POneType = _p.POneType;
            target.PreviousProjectID = _p.PreviousProjectID;
            target.ProblemStatement = _p.ProblemStatement;
            target.PreviousProject = _p.PreviousProject;
            target.ProjectNr = _p.ProjectNr;
            target.Projects = target.Projects;
            target.PTwoTeamSize = _p.PTwoTeamSize;
            target.PTwoType = _p.PTwoType;
            target.PublishedDate = _p.PublishedDate;
            target.References = _p.References;
            target.Remarks = _p.Remarks;
            target.Reservation1Mail = _p.Reservation1Mail;
            target.Reservation1Name = _p.Reservation1Name;
            target.Reservation2Mail = _p.Reservation2Mail;
            target.Reservation2Name = _p.Reservation2Name;
            target.Semester = _p.Semester;
            target.State = _p.State;
            target.Tasks = _p.Tasks;
            target.TypeAppWeb = _p.TypeAppWeb;
            target.TypeCGIP = _p.TypeCGIP;
            target.TypeDBBigData = _p.TypeDBBigData;
            target.TypeDesignUX = _p.TypeDesignUX;
            target.TypeHW = _p.TypeHW;
            target.TypeMlAlg = _p.TypeMlAlg;
            target.TypeSE = _p.TypeSE;
            target.TypeSysSec = _p.TypeSysSec;
            target.UnderNDA = _p.UnderNDA;
            target.WebSummaryChecked = _p.WebSummaryChecked;
            target.GradeSentToAdmin = _p.GradeSentToAdmin;
            target.Notes = _p.Notes;
        }

        public static Project CreateNewProject(ProStudentCreatorDBDataContext db)
        {
            Project project = new Project();
            project.InitNew();
            db.Projects.InsertOnSubmit(project);
            project.ModificationDate = DateTime.Now;
            project.LastEditedBy = ShibUser.GetEmail();
            return project;
        }

        /// <summary>
        ///     Generates a new project number that is unique per semester and institute.
        ///     Applies to projects after submission.
        /// </summary>
        /// <param name="_p"></param>
        public static void AssignUniqueProjectNr(this Project _p, ProStudentCreatorDBDataContext dbx)
        {
            var semesterStart = Semester.ActiveSemester(_p.PublishedDate, dbx).StartDate;
            var semesterEnd = Semester.ActiveSemester(_p.PublishedDate, dbx).DayBeforeNextSemester;

            // Get project numbers from this semester & same department
            var nrs = (
                from p in dbx.Projects
                where p.PublishedDate >= semesterStart && p.PublishedDate <= semesterEnd
                      && p.Id != _p.Id
                      && (p.State == ProjectState.Published || p.State == ProjectState.Submitted)
                      && p.Department == _p.Department
                select p.ProjectNr).ToArray();
            if (_p.ProjectNr >= 100 || nrs.Contains(_p.ProjectNr) || _p.ProjectNr < 1)
            {
                _p.ProjectNr = 1;
                while (nrs.Contains(_p.ProjectNr))
                    _p.ProjectNr++;
            }

        }

        #endregion

        #region Getters

        public static int GetProjectTeamSize(this Project _p)
        {
            if (_p.POneTeamSize.Size2 ||
                _p.PTwoTeamSize != null && _p.PTwoTeamSize.Size2)
                return 2;
            return 1;
        }

        public static DateTime? GetDeliveryDate(this Project _p)
        {
            using (var db = new ProStudentCreatorDBDataContext())
            {
                DateTime dbDate;

                if (_p.LogProjectDuration == 1 && (_p.LogProjectType?.P5 ?? false)) //IP5 Projekt Voll/TeilZeit
                    return DateTime.TryParseExact(_p.Semester.SubmissionIP5FullPartTime, "dd.MM.yyyy",
                        CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dbDate)
                        ? dbDate : (DateTime?)null;
                if (_p.LogProjectDuration == 2 && (_p.LogProjectType?.P5 ?? false)) //IP5 Berufsbegleitend
                    return DateTime.TryParseExact(_p.Semester.SubmissionIP5Accompanying, "dd.MM.yyyy",
                        CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dbDate)
                        ? dbDate : (DateTime?)null;
                if (_p.LogProjectDuration == 1 && (_p.LogProjectType?.P6 ?? false)) //IP6 Variante 1 Semester
                    return DateTime.TryParseExact(_p.Semester.SubmissionIP6Normal, "dd.MM.yyyy",
                        CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dbDate)
                        ? dbDate : (DateTime?)null;
                if (_p.LogProjectDuration == 2 && (_p.LogProjectType?.P6 ?? false)) //IP6 Variante 2 Semester
                    return DateTime.TryParseExact(_p.Semester.SubmissionIP6Variant2, "dd.MM.yyyy",
                        CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dbDate)
                        ? dbDate : (DateTime?)null;

                return null;
            }
        }

        public static string Student1FirstName(this Project _p) => _p.LogStudent1Name?.Split(' ')?[0];
        public static string Student2FirstName(this Project _p) => _p.LogStudent2Name?.Split(' ')?[0];

        public static string Student1LastName(this Project _p) => _p.LogStudent1Name == null ? null : string.Join(" ", _p.LogStudent1Name.Split(' ').Skip(1).ToArray());
        public static string Student2LastName(this Project _p) => _p.LogStudent2Name == null ? null : string.Join(" ", _p.LogStudent2Name.Split(' ').Skip(1).ToArray());


        public static bool RightAmountOfTopics(this Project _p)
        {
            var topics = _p.GetProjectTypeBools();
            var c = topics.Where(b => b).Count();
            return (c == 1 || c == 2);
        }

        public static bool MinimalClientInformationProvided(this Project _p)
        {
            if (_p.ClientType == (int)ClientType.Internal) return true;
            if (string.IsNullOrWhiteSpace(_p.ClientMail)) return false;
            if (_p.ClientType == (int)ClientType.Company && string.IsNullOrWhiteSpace(_p.ClientCompany)) return false;
            return true;
        }

        public static bool IsAtLeastPublished(this Project _p)
        {
            return _p.State == ProjectState.Published
                || _p.State == ProjectState.Ongoing
                || _p.State == ProjectState.Finished
                || _p.State == ProjectState.Canceled
                || _p.State == ProjectState.ArchivedFinished
                || _p.State == ProjectState.ArchivedCanceled;
        }

        public static bool WasDefenseHeld(this Project _p)
        {
            return _p.IsMainVersion && _p.LogGradeStudent1 != null && _p.BillingStatus?.RequiresProjectResults == true && _p.State == ProjectState.Published;

            //    return true;

            //var def = _p.GetDefenseDate();
            //if (def == null)
            //    return false;

            //return _p.BillingStatus != null && _p.IsMainVersion && _p.LogGradeStudent1 != null && def.Value.AddDays(1) < DateTime.Now;
        }

        public static DateTime? GetDefenseDate(this Project _p)
        {
            if (_p.LogDefenceDate != null)
                return _p.LogDefenceDate;

            DateTime dbDate;
            if (_p.LogProjectDuration == 1 && (_p?.LogProjectType?.P5 ?? false)) //IP5 Projekt Voll/TeilZeit
                return DateTime.TryParseExact(_p.Semester.SubmissionIP5FullPartTime, "dd.MM.yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dbDate)
                    ? dbDate + Global.ExpectFinalPresentationAfterSubmissionForIP5 : (DateTime?)null;
            if (_p.LogProjectDuration == 2 && (_p?.LogProjectType?.P5 ?? false)) //IP5 Berufsbegleitend
                return DateTime.TryParseExact(_p.Semester.SubmissionIP5Accompanying, "dd.MM.yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dbDate)
                    ? dbDate + Global.ExpectFinalPresentationAfterSubmissionForIP5 : (DateTime?)null;

            return null;
        }


        public static bool CanEditTitle(this Project _p)
        {
            return (_p.LogProjectType?.P5 == true) || (DateTime.Now < _p.GetDeliveryDate() - Global.AllowTitleChangesBeforeSubmission);
        }


        public static Semester GetEndSemester(this Project _p, ProStudentCreatorDBDataContext db)
        {
            return _p.LogProjectDuration == 2 && _p.LogProjectType.P6 ? Semester.NextSemester(_p.Semester, db) : _p.Semester;
        }

        public static string ExhibitionBachelorThesis(this Project _p, ProStudentCreatorDBDataContext db)
        {
            if (_p.LogProjectType?.P5 == true
                || (_p.LogProjectType?.P6 == true && _p.LogProjectDuration == 1 && !_p.Semester.IsSpringSemester())
                || (_p.LogProjectType?.P6 == true && _p.LogProjectDuration == 2 && _p.Semester.IsSpringSemester()))
            {
                return "";
            }
            return _p.GetEndSemester(db).ExhibitionBachelorThesis;
        }


        public static bool ShouldBeGradedByNow(this Project p, ProStudentCreatorDBDataContext db, DateTime _cutoffDate)
        {
            var date = p.GetDefenseDate();
            if (!date.HasValue)
                return false;

            var gradeDate = date.Value + Global.GradingDuration;
            if (gradeDate <= _cutoffDate)
                return false;

            return gradeDate <= DateTime.Now;
        }

        #endregion

        #region State Transitions

        /// <summary>
        ///     Submits user's project for approval by an admin.
        /// </summary>
        /// <param name="_p"></param>
        public static void Submit(this Project _p, ProStudentCreatorDBDataContext _dbx)
        {
            if (!CheckTransitionSubmit(_p)) HandleInvalidState(_p, "Submit");
            
            _p.ModificationDate = DateTime.Now;
            AssignUniqueProjectNr(_p, _dbx);
            _p.State = ProjectState.Submitted;
        }

        /// <summary>
        ///     Rolls back project into editable state.
        /// </summary>
        /// <param name="_p"></param>
        public static void Unsubmit(this Project _p)
        {
            if (!CheckTransitionUnsubmit(_p)) HandleInvalidState(_p, "Unsubmit");

            _p.State = ProjectState.InProgress;
        }

        /// <summary>
        ///     Rejects a project after submission. Admin only.
        /// </summary>
        /// <param name="_p"></param>
        public static void Reject(this Project _p)
        {
            if (!CheckTransitionReject(_p)) HandleInvalidState(_p, "Reject");

            _p.State = ProjectState.Rejected;
        }

        /// <summary>
        ///     Publishes a project after submission. Admin only.
        /// </summary>
        /// <param name="_p"></param>
        public static void Publish(this Project _p)
        {
            if (!CheckTransitionPublish(_p)) HandleInvalidState(_p, "Publish");
            
            _p.PublishedDate = DateTime.Now;
            _p.ModificationDate = DateTime.Now;
            _p.State = ProjectState.Published;
        }

        /// <summary>
        ///     Starts the project e.g. the students are working on it
        /// </summary>
        /// <param name="_p"></param>
        public static void Kickoff(this Project _p)
        {
            if (!CheckTransitionKickoff(_p)) HandleInvalidState(_p, "Kickoff");

            _p.ModificationDate = DateTime.Now;
            _p.State = ProjectState.Ongoing;
        }

        /// <summary>
        ///     Finish the project
        /// </summary>
        /// <param name="_p"></param>
        public static void Finish(this Project _p)
        {
            if (!CheckTransitionFinish(_p)) HandleInvalidState(_p, "Finish");

            _p.ModificationDate = DateTime.Now;
            _p.State = ProjectState.Finished;
        }

        /// <summary>
        ///     Cancel the project
        /// </summary>
        /// <param name="_p"></param>
        public static void Cancel(this Project _p)
        {
            if (!CheckTransitionCancel(_p)) HandleInvalidState(_p, "Cancel");

            _p.ModificationDate = DateTime.Now;
            _p.State = ProjectState.Canceled;
        }

        /// <summary>
        ///     Archive the project after everything is done
        /// </summary>
        /// <param name="_p"></param>
        public static void Archive(this Project _p)
        {
            if (!CheckTransitionArchive(_p)) HandleInvalidState(_p, "Archive");

            if (_p.State == ProjectState.Finished) _p.State = ProjectState.ArchivedFinished;
            if (_p.State == ProjectState.Canceled) _p.State = ProjectState.ArchivedCanceled;
            _p.ModificationDate = DateTime.Now;
        }

        /// <summary>
        ///     Sets project state to deleted so it's no longer listed. Admin only.
        /// </summary>
        /// <param name="_p"></param>
        public static void Delete(this Project _p)
        {
            if (!CheckTransitionDelete(_p)) HandleInvalidState(_p, "Delete");

            _p.ModificationDate = DateTime.Now;
            _p.State = ProjectState.Deleted;
        }

        #endregion

        #region Transition Checks

        public static bool CheckTransitionSubmit(this Project _p)
        {
            // Permission
            return UserHasAdvisor1Rights(_p)
                // Name
                && !string.IsNullOrWhiteSpace(_p.Name)
                // Advisor1
                && _p.Advisor1 != null
                // 1-2 Topics
                && _p.RightAmountOfTopics()
                // Client Information (Depending on ClientType)
                && _p.MinimalClientInformationProvided()
                // Reservation (Depending on PreviousProject)
                && (_p.PreviousProject == null || (_p.PreviousProject != null && _p.Reservation1Mail == _p.PreviousProject.LogStudent1Mail && _p.Reservation2Mail == _p.PreviousProject.LogStudent2Mail));
        }

        public static bool CheckTransitionUnsubmit(this Project _p)
        {
            // Permission
            return UserHasCreatorRights(_p);
        }

        public static bool CheckTransitionReject(this Project _p)
        {
            // Permission
            return UserHasDepartmentManagerRights(_p)
                // Ablehnungsgrund
                && !string.IsNullOrWhiteSpace(_p.Ablehnungsgrund);
        }

        public static bool CheckTransitionPublish(this Project _p)
        {
            // Permission
            return UserHasDepartmentManagerRights(_p)
                // Name
                && !string.IsNullOrWhiteSpace(_p.Name)
                // Advisor1
                && _p.Advisor1 != null
                // 1-2 Topics
                && _p.RightAmountOfTopics()
                // Client Information (Depending on ClientType)
                && _p.MinimalClientInformationProvided()
                // Reservation (Depending on PreviousProject)
                && (_p.PreviousProject == null || (_p.PreviousProject != null && _p.Reservation1Mail == _p.PreviousProject.LogStudent1Mail && _p.Reservation2Mail == _p.PreviousProject.LogStudent2Mail));
        }

        public static bool CheckTransitionKickoff(this Project _p)
        {
            // Permission
            return UserHasDepartmentManagerRights(_p)
                // ProjectType
                && (_p.LogProjectType.P5 || _p.LogProjectType.P6)
                // ProjectDuration
                && _p.LogProjectDuration.HasValue
                // Student1Name
                && !string.IsNullOrWhiteSpace(_p.LogStudent1Name)
                // Student1Mail
                && !string.IsNullOrWhiteSpace(_p.LogStudent1Mail);
        }

        public static bool CheckTransitionFinish(this Project _p)
        {
            // Permission
            return UserHasAdvisor1Rights(_p)
                // - Language
                && ((_p.LogLanguageEnglish.HasValue && (bool)_p.LogLanguageEnglish) || (_p.LogLanguageGerman.HasValue && (bool)_p.LogLanguageGerman))
                // - Client Information (Depending on ClientType)
                && _p.MinimalClientInformationProvided()
                // - GradeStudent1
                && _p.LogGradeStudent1.HasValue
                // - GradeStudent2
                && (string.IsNullOrWhiteSpace(_p.LogStudent2Mail) || (!string.IsNullOrWhiteSpace(_p.LogStudent2Mail) && _p.LogGradeStudent2.HasValue))
                // - WebSummaryChecked
                && _p.WebSummaryChecked
                // - Expert
                && (!_p.LogProjectType.P6 || (_p.LogProjectType.P6 && _p.Expert != null));
        }

        public static bool CheckTransitionCancel(this Project _p)
        {
            // Permission
            return UserHasAdvisor1Rights(_p)
                // - GradeStudent1
                && _p.LogGradeStudent1.HasValue
                // - GradeStudent2
                && (string.IsNullOrWhiteSpace(_p.LogStudent2Mail) || (!string.IsNullOrWhiteSpace(_p.LogStudent2Mail) && _p.LogGradeStudent2.HasValue))
                // - BillingStatus
                && _p.BillingStatus != null;
        }

        public static bool CheckTransitionArchive(this Project _p)
        {
            switch (_p.State)
            {
                case ProjectState.Finished:
                    // ExpertPaid
                    return _p.LogExpertPaid
                    // GradeSentToAdmin
                    && _p.GradeSentToAdmin;

                case ProjectState.Canceled:
                    // GradeSentToAdmin
                    return _p.GradeSentToAdmin;

                default:
                    return false;
            }
        }
        public static bool CheckTransitionDelete(this Project _p)
        {
            // Permission
            return UserHasDepartmentManagerRights(_p) || ((_p.State == ProjectState.InProgress || _p.State == ProjectState.Submitted || _p.State == ProjectState.Rejected) && UserHasCreatorRights(_p));
        }

        #endregion

        #region Roles

        public static bool UserIsCreator(this Project _p)
        {
            return ShibUser.GetEmail() == _p.Creator;
        }

        public static bool UserIsAdvisor2(this Project _p)
        {
            return ShibUser.GetEmail() == _p.Advisor2?.Mail;
        }

        public static bool UserIsAdvisor1(this Project _p)
        {
            return ShibUser.GetEmail() == _p.Advisor1?.Mail;
        }

        public static bool UserIsDepartmentManager(this Project _p)
        {
            return ShibUser.IsDepartmentManager(_p.Department);
        }

        public static bool UserIsOwner(this Project _p)
        {
            return _p.UserIsCreator() || _p.UserIsAdvisor2() || _p.UserIsAdvisor1();
        }

        #endregion

        #region Rights

        public static bool UserHasDepartmentManagerRights(this Project _p)
        {
            return UserIsDepartmentManager(_p) || ShibUser.GetEmail() == Global.WebAdmin;
        }

        public static bool UserHasAdvisor1Rights(this Project _p)
        {
            return UserIsAdvisor1(_p) || UserHasDepartmentManagerRights(_p);
        }

        public static bool UserHasAdvisor2Rights(this Project _p)
        {
            return UserIsAdvisor2(_p) || UserHasAdvisor1Rights(_p);
        }

        public static bool UserHasCreatorRights(this Project _p)
        {
            return UserIsCreator(_p) || UserHasAdvisor2Rights(_p);
        }

        #endregion

        #region Permissions

        public static bool UserCanEdit(this Project _p)
        {
            if (ShibUser.CanEditAllProjects()) return true;

            switch (_p.State)
            {
                case ProjectState.InProgress:
                case ProjectState.Submitted:
                case ProjectState.Rejected:
                    return _p.UserHasCreatorRights();
                case ProjectState.Published:
                case ProjectState.Ongoing:
                case ProjectState.Finished:
                case ProjectState.Canceled:
                case ProjectState.ArchivedFinished:
                case ProjectState.ArchivedCanceled:
                    return _p.UserHasDepartmentManagerRights();
                default:
                    return false;
            }
        }

        public static bool UserCanEditAfterStart(this Project _p)
        {
            if (ShibUser.CanEditAllProjects()) return true;

            switch (_p.State)
            {
                case ProjectState.InProgress:
                case ProjectState.Submitted:
                case ProjectState.Rejected:
                    return _p.UserHasDepartmentManagerRights();
                case ProjectState.Published:
                case ProjectState.Ongoing:
                case ProjectState.Finished:
                case ProjectState.Canceled:
                case ProjectState.ArchivedFinished:
                case ProjectState.ArchivedCanceled:
                    return _p.UserHasAdvisor2Rights();
                default:
                    return false;
            }
        }

        public static bool UserCanSubmit(this Project _p)
        {
            return (_p.State == ProjectState.InProgress || _p.State == ProjectState.Rejected)
                && (ShibUser.CanSubmitAllProjects() || _p.UserHasAdvisor1Rights());
        }

        public static bool UserCanUnsubmit(this Project _p)
        {
            return _p.State == ProjectState.Submitted
                && (ShibUser.CanSubmitAllProjects() || _p.UserHasAdvisor1Rights());
        }

        public static bool UserCanPublish(this Project _p)
        {
            return _p.State == ProjectState.Submitted
                && (ShibUser.CanPublishProject() || _p.UserHasDepartmentManagerRights());
        }

        public static bool UserCanUnpublish(this Project _p)
        {
            return _p.State == ProjectState.Published
                && (ShibUser.CanPublishProject() || _p.UserHasDepartmentManagerRights());
        }

        public static bool UserCanReject(this Project _p)
        {
            return (_p.State == ProjectState.Submitted || _p.State == ProjectState.Published)
                && (ShibUser.CanPublishProject() || _p.UserHasDepartmentManagerRights());
        }

        public static bool UserCanKickoff(this Project _p)
        {
            return _p.State == ProjectState.Published
                && _p.UserHasDepartmentManagerRights();
        }

        public static bool UserCanFinish(this Project _p)
        {
            return _p.State == ProjectState.Ongoing
                && _p.UserHasAdvisor1Rights();
        }

        public static bool UserCanCancel(this Project _p)
        {
            return _p.State == ProjectState.Ongoing
                && _p.UserHasAdvisor1Rights();
        }

        public static bool UserCanDelete(this Project _p)
        {
            return (_p.State == ProjectState.InProgress || _p.State == ProjectState.Submitted || _p.State == ProjectState.Rejected)
                && _p.UserHasCreatorRights();

        }

        #endregion

        public static void HandleInvalidState(Project _p, string msg)
        {
#if !DEBUG
            using (var smtpClient = new SmtpClient())
            {
                var mail = new MailMessage { From = new MailAddress("noreply@fhnw.ch") };
                mail.To.Add(new MailAddress(Global.WebAdmin));
                mail.Subject = "InvalidStateError";
                mail.IsBodyHtml = true;

                var mailMessage = new StringBuilder();
                mailMessage.Append("<div style=\"font-family: Arial\">");
                mailMessage.Append(
                    $"<p>Time: {DateTime.Now}<p>" +
                    $"<p>User: {ShibUser.GetEmail()}<p>" +
                    $"<p>Project: {_p.GetFullTitle()}<p>" +
                    $"<p>ProjectNr: {_p.Id}<p>" +
                    $"<p>Message: {msg}<p>"
                );
                mail.Body = mailMessage.ToString();
                smtpClient.Send(mail);
            }
#endif
            throw new InvalidOperationException(msg);
        }
    }
    public partial class Project
    {
        public static string GetStateColor(int _state)
        {
            switch (_state)
            {
                case ProjectState.InProgress:
                    return "#CEECF5";

                case ProjectState.Submitted:
                    return "#ffcc99";

                case ProjectState.Rejected:
                    return "#F5A9A9";

                case ProjectState.Published:
                    return "#A9F5A9";

                case ProjectState.Ongoing:
                    return "#A9F5A9";
                    //return "#64ed64";

                case ProjectState.Finished:
                case ProjectState.ArchivedFinished:
                    return "#A9F5A9";
                    //return "#1adb1a";

                case ProjectState.Canceled:
                case ProjectState.ArchivedCanceled:
                    return "#A9F5A9";
                    //return "#e8463b";

                case ProjectState.Deleted:
                    return "#919191";

                default:
                    throw new Exception();

            }
        }

        public static string GetStateAsString(int _state)
        {
            switch (_state)
            {
                case ProjectState.InProgress:
                    return "In Bearbeitung";

                case ProjectState.Submitted:
                    return "Eingereicht";

                case ProjectState.Rejected:
                    return "Abgelehnt";

                case ProjectState.Published:
                    return "Veröffentlicht";

                case ProjectState.Ongoing:
                    return "In Durchführung";

                case ProjectState.Finished:
                case ProjectState.ArchivedFinished:
                    return "Abgeschlossen";

                case ProjectState.Canceled:
                case ProjectState.ArchivedCanceled:
                    return "Abgebrochen";

                case ProjectState.Deleted:
                    return "Gelöscht";

                default:
                    throw new Exception();

            }
        }

        public string StateColor
        {
            get
            {
                return Project.GetStateColor(State);
            }
        }


        public string StateAsString
        {
            get
            {
                return Project.GetStateAsString(State);
            }
        }
    }
}