﻿using System;
using System.Data;
using System.Data.Linq;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Web.Services.Description;
using System.Web.UI;
using System.Web.UI.WebControls;
using NPOI.OpenXml4Net.Exceptions;
using NPOI.SS.Formula.Functions;

namespace ProStudCreator
{
    public partial class ProjectInfoPage : Page
    {
        private ProStudentCreatorDBDataContext db = new ProStudentCreatorDBDataContext();
        private int? id;
        private Project project;
        private ProjectType projectPriority = new ProjectType();
        private DateTime today = DateTime.Now;
        private DateTime deliveryDate;
        private bool canPostEdit = false;

        private enum ProjectTypes
        {
            IP5,
            IP6,
            NotDefined
        }

        private ProjectTypes type = ProjectTypes.NotDefined;

        protected void Page_Load(object sender, EventArgs e)
        {

            canPostEdit = SemesterDropdown.Enabled =
            (ShibUser.IsAdmin() || ShibUser.GetEmail() == project.Advisor1Mail ||
             ShibUser.GetEmail() == project.Advisor2Mail);

            // Retrieve the project from DB
            if (Request.QueryString["id"] != null)
            {
                id = int.Parse(Request.QueryString["id"]);
                project = db.Projects.Single((Project p) => (int?)p.Id == id);
            }
            else
            {
                Response.Redirect("Projectlist.aspx");
                Response.End();
            }

            if (Page.IsPostBack) return;
            project.Semester = project.Semester ?? Semester.NextSemester(db);

            ProjectTitle.Text = project.Name;


            Student1Name.Text = (!string.IsNullOrEmpty(project.LogStudent1Name))
                ? "<a href=\"mailto:" + project.LogStudent1Mail + "\">" +
                  Server.HtmlEncode(project.LogStudent1Name).Replace(" ", "&nbsp;") + "</a>"
                : "?";
            Student2Name.Text = (!string.IsNullOrEmpty(project.LogStudent2Name))
                ? "<a href=\"mailto:" + project.LogStudent2Mail + "\">" +
                  Server.HtmlEncode(project.LogStudent2Name).Replace(" ", "&nbsp;") + "</a>"
                : "";

            Advisor1Name.Text = (!string.IsNullOrEmpty(project.Advisor1Name))
                ? "<a href=\"mailto:" + project.Advisor1Mail + "\">" +
                  Server.HtmlEncode(project.Advisor1Name).Replace(" ", "&nbsp;") + "</a>"
                : "?";
            Advisor2Name.Text = (!string.IsNullOrEmpty(project.Advisor2Name))
                ? "<a href=\"mailto:" + project.Advisor2Mail + "\">" +
                  Server.HtmlEncode(project.Advisor2Name).Replace(" ", "&nbsp;") + "</a>"
                : "";

            if (project.LogProjectType != null)
            {
                if (project.LogProjectType.P5 && !project.LogProjectType.P6)
                {
                    ExpertName.Text = "Bei IP5 Projekten gibt es keinen Experten.";
                }
                else if (!project.LogProjectType.P5 && project.LogProjectType.P6)
                {
                    ExpertName.Text = (!string.IsNullOrEmpty(project.LogExpertID.ToString()))
                        ? "<a href=\"mailto:" + project.Expert.Mail + "\">" +
                          Server.HtmlEncode(project.Expert.Name).Replace(" ", "&nbsp;") + "</a>"
                        : "Wird noch organisiert.";
                }
                else
                {
                    ExpertName.Text = "Noch nicht entschieden.";
                }
            }
            else
            {
                ExpertName.Text = "Noch nicht entschieden.";
            }


            if (project.LogProjectTypeID == null)
            {
                lblProjectType.Text = "?";
                type = ProjectTypes.NotDefined;
            }
            else if (project.LogProjectType.P5 && !project.LogProjectType.P6)
            {
                lblProjectType.Text = "IP5";
                type = ProjectTypes.IP5;
            }
            else if (!project.LogProjectType.P5 && project.LogProjectType.P6)
            {
                lblProjectType.Text = "IP6";
                type = ProjectTypes.IP6;
            }
            else
            {
                lblProjectType.Text = "?";
                type = ProjectTypes.NotDefined;
            }


            if (project?.LogProjectDuration == 2)
            {
                lblProjectDuration.Text = "2 Semester";
            }
            else if (project?.LogProjectDuration == 1)
            {
                lblProjectDuration.Text = "1 Semester";
            }
            else
            {
                lblProjectDuration.Text = "?";
            }

            ProjectEndPresentation.Text = project.LogDefenceDate?.ToString() ?? "?";

            //darf erst Hier aufgerufen werden.
            deliveryDate = SetDates();

            ProjectExhibition.Text = project.Semester.ExhibitionBachelorThesis;

            //Disable the Textbox if Title can't be changed anymore
            ProjectTitle.Enabled =
                (ShibUser.IsAdmin() || ShibUser.GetEmail() == project.Advisor1Mail ||
                 ShibUser.GetEmail() == project.Advisor2Mail)
                && deliveryDate.AddDays(-28) > today;

            //Disable the dropdowns for unauthorized users
            drpLogLanguage.Enabled = canPostEdit;
            if (project.LogLanguageEnglish != null && project.LogLanguageGerman != null)
            {
                if (project.LogLanguageEnglish.Value && !project.LogLanguageGerman.Value)
                {
                    drpLogLanguage.SelectedValue = "1";
                }
                else
                {
                    drpLogLanguage.SelectedValue = "2";
                }
            }
            else
            {
                drpLogLanguage.SelectedValue = "0";
            }


            nbrGrade.Enabled = canPostEdit;
            nbrGrade.Text = string.Concat(project?.LogGrade);


            SemesterDropdown.Enabled = canPostEdit;
            SemesterDropdown.DataSource = db.BillingStatus;
            SemesterDropdown.DataBind();
            SemesterDropdown.Items.Insert(0, new ListItem("Unbekannt"));
            SemesterDropdown.SelectedIndex = project.BillingStatusID ?? 0;


            if (project.LogProjectType != null && project.LogProjectType.P5)
            {
                lblAussstellungBachelorthese.Visible = false;
                ProjectExhibition.Visible = false;
            }
        }

        private DateTime SetDates()
        {
            DateTime dbDate;
            if (project?.LogProjectDuration == 1 && type == ProjectTypes.IP5) //IP5 Projekt Voll/TeilZeit
            {
                ProjectDelivery.Text = project.Semester.SubmissionIP5FullPartTime;
                lblProjectEndPresentation.Text = "Schlusspräsentation:";
                ProjectEndPresentation.Text =
                    "Die Studierenden sollen die Schlusspräsentation (Termin, Ort, Auftraggeber) selbständig organisieren.";

                return DateTime.TryParseExact(project.Semester.SubmissionIP5FullPartTime, "dd.MM.yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dbDate)
                    ? dbDate
                    : Semester.NextSemester(db).EndDate;
            }
            else if (project?.LogProjectDuration == 2 && type == ProjectTypes.IP5) //IP5 Berufsbegleitend
            {
                ProjectDelivery.Text = project.Semester.SubmissionIP5Accompanying;
                lblProjectEndPresentation.Text = "Schlusspräsentation:";
                ProjectEndPresentation.Text =
                    "Die Studierenden sollen die Schlusspräsentation (Termin, Ort, Auftraggeber) selbständig organisieren.";

                return DateTime.TryParseExact(project.Semester.SubmissionIP5Accompanying, "dd.MM.yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dbDate)
                    ? dbDate
                    : Semester.NextSemester(db).EndDate;
            }
            else if (project?.LogProjectDuration == 1 && type == ProjectTypes.IP6) //IP6 Variante 1 Semester
            {
                ProjectDelivery.Text = project.Semester.SubmissionIP6Normal;
                lblProjectEndPresentation.Text = "Schlusspräsentation:";

                return DateTime.TryParseExact(project.Semester.SubmissionIP6Normal, "dd.MM.yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dbDate)
                    ? dbDate
                    : Semester.NextSemester(db).EndDate;
            }
            else if (project?.LogProjectDuration == 2 && type == ProjectTypes.IP6) //IP6 Variante 2 Semester
            {
                ProjectDelivery.Text = project.Semester.SubmissionIP6Variant2;
                lblProjectEndPresentation.Text = "Verteidigung:";

                return DateTime.TryParseExact(project.Semester.SubmissionIP6Variant2, "dd.MM.yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dbDate)
                    ? dbDate
                    : Semester.NextSemester(db).EndDate;
            }
            else
            {
                ProjectDelivery.Text = "?";
                lblProjectEndPresentation.Text = "Schlusspräsentation:";
                ProjectEndPresentation.Text = "?";
                return Semester.NextSemester(db).EndDate;
            }
        }

        private void ReturnAlert(String message)
        {
            var sb = new StringBuilder();
            sb.Append("<script type = 'text/javascript'>");
            sb.Append("window.onload=function(){");
            sb.Append("alert('");
            sb.Append(message);
            sb.Append("')};");
            sb.Append("</script>");
            ClientScript.RegisterClientScriptBlock(base.GetType(), "alert", sb.ToString());
        }

        protected void BtnSaveChanges_OnClick(object sender, EventArgs e)
        {
            var oldTitle = project.Name;
            if (canPostEdit)
            {
                project.Name = ProjectTitle.Text.FixupParagraph();
                db.SubmitChanges();
                project.OverOnePage = (new PdfCreator().CalcNumberOfPages(project) > 1);
                if (project.OverOnePage)
                {
                    project.Name = oldTitle;
                    db.SubmitChanges();
                    ReturnAlert("Die Änderung des Titels ist nicht möglich, weil das PDF zu lang werden würde.");
                    Response.Redirect("ProjectInfoPage?id=" + project.Id);
                }
                else
                {
                    //methods for the fileuploads
                    StreamAllFilesToDb();

                    if (nbrGrade.Text != "")
                    {
                        project.LogGrade = float.Parse(nbrGrade.Text);
                    }

                    switch (drpLogLanguage.SelectedValue)
                    {
                        case "1":
                            project.LogLanguageEnglish = true;
                            project.LogLanguageGerman = false;
                            break;
                        case "2":
                            project.LogLanguageEnglish = false;
                            project.LogLanguageGerman = true;
                            break;
                        default:
                            project.LogLanguageGerman = null;
                            project.LogLanguageEnglish = null;
                            break;
                    }

                    project.BillingStatusID = (SemesterDropdown.SelectedIndex == 0)
                        ? (int?)null
                        : int.Parse(SemesterDropdown.SelectedValue);
                    project.Name = ProjectTitle.Text.FixupParagraph();
                    project.ModificationDate = DateTime.Now;
                    project.LastEditedBy = ShibUser.GetEmail();
                    db.SubmitChanges();
                    Response.Redirect("ProjectInfoPage?id=" + project.Id);
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        protected void BtnCancel_OnClick(object sender, EventArgs e)
        {
            Response.Redirect("Projectlist");
        }

        protected void btnDeleteDoc_OnClick(object sender, EventArgs e)
        {
            DeleteFile("ProjectDocument");
            Response.Redirect("ProjectInfoPage?id=" + project.Id);
        }

        protected void btnDeletePresentation_OnClick(object sender, EventArgs e)
        {
            DeleteFile("ProjectPresentation");
            Response.Redirect("ProjectInfoPage?id=" + project.Id);
        }

        protected void btnDeleteCode_OnClick(object sender, EventArgs e)
        {
            DeleteFile("ProjectCode");
            Response.Redirect("ProjectInfoPage?id=" + project.Id);
        }

        private void StreamAllFilesToDb()
        {
            //if (fuAddCode.HasFile)
            //{
            //    using (var input = fuAddCode.PostedFile.InputStream)
            //    {
            //        StreamFiletoDb(input, "ProjectCode");
            //    }
            //}

            //if (fuAddDoc.HasFile)
            //{
            //    using (var input = fuAddDoc.PostedFile.InputStream)
            //    {
            //        StreamFiletoDb(input, "ProjectDocumentation");
            //    }
            //}

            //if (fuAddPresentation.HasFile)
            //{
            //    using (var input = fuAddPresentation.PostedFile.InputStream)
            //    {
            //        StreamFiletoDb(input, "ProjectPresentation");
            //    }
            //}
        }

        private void StreamFiletoDb(Stream input, string columnname)
        {
            using (var connection = new SqlConnection())
            {
                var cmd =
                    new SqlCommand(
                        $"INSERT INTO Projects(ROWGUID, {columnname}) VALUES(newsequentialid(),CAST('' AS varbinary(max))) WHERE Id = {project.Id};")
                    {
                        CommandType = CommandType.Text,
                        Connection = connection
                    };
                connection.Open();
                cmd.ExecuteNonQuery();
            }


            using (var connection = new SqlConnection())
            {
                connection.Open();

                var command =
                    new SqlCommand(
                        $"SELECT TOP(1) {columnname}.PathName(), GET_FILESTREAM_TRANSACTION_CONTEXT() FROM BothTable WHERE Id = {project.Id}",
                        connection);

                var tran = connection.BeginTransaction(IsolationLevel.ReadCommitted);
                command.Transaction = tran;

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // Get the pointer for file
                        var path = reader.GetString(0);
                        var transactionContext = reader.GetSqlBytes(1).Buffer;

                        using (
                            Stream fileStream = new SqlFileStream(path, transactionContext, FileAccess.Write,
                                FileOptions.SequentialScan, allocationSize: 0))
                        {
                            input.CopyTo(fileStream, 65536);
                        }
                    }
                }
                tran.Commit();
            }
        }

        private void DeleteFile(string columnname)
        {
            using (var connection = new SqlConnection())
            {
                connection.Open();

                var command =
                    new SqlCommand($"INSERT INTO Prostud.dbo.Projects({columnname}) VALUES(NULL) WHERE Id = {project.Id}", connection);

                var tran = connection.BeginTransaction(IsolationLevel.ReadCommitted);
                command.Transaction = tran;
                tran.Commit();
            }
        }
    }
}