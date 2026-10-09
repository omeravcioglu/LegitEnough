using System;
using System.Collections.Generic;
using LegitEnough.Sim;

namespace LegitEnough.SimTests
{
    /// <summary>Plays the simulation programmatically, the way a player would through the UI.</summary>
    public static class Bot
    {
        public const float Step = 0.05f;

        public static float TickUntil(GameSession s, Func<bool> cond, float maxSeconds, string what)
        {
            float t = 0f;
            while (!cond())
            {
                s.Tick(Step);
                t += Step;
                if (t > maxSeconds) throw new AssertException($"timed out after {maxSeconds}s waiting for: {what} (phase {s.Phase}, {s.Now})");
            }
            return t;
        }

        public static void Tick(GameSession s, float seconds)
        {
            for (float t = 0f; t < seconds; t += Step) s.Tick(Step);
        }

        public static Visit WaitForChoice(GameSession s, string choiceId, float max = 120f)
        {
            Visit found = null;
            TickUntil(s, () =>
            {
                found = s.Visits.Find(v => v.Phase == VisitPhase.Choosing && v.Choices.Exists(c => c.Id == choiceId && c.Enabled));
                return found != null;
            }, max, $"choice '{choiceId}'");
            return found;
        }

        /// <summary>Does everything a careful player does at the photo station. Returns the kept photo.</summary>
        public static PhotoCapture TakeGoodPhoto(GameSession s, Visit v, bool careful = true)
        {
            var session = s.BeginPhotoSession(v.Id);
            Assert.True(session != null, "photo session should start");
            var rules = s.Country(v.Order.CountryId).Spec.Photo;
            var subj = session.Subject;

            if (careful)
            {
                int guard = 0;
                while (subj.HasGlasses && (!rules.GlassesAllowed || subj.WearingSunglasses) && guard++ < 4)
                    subj.Command(PhotoCommand.RemoveGlasses);
                if (subj.HatOn && !rules.HeadwearAllowed) subj.Command(PhotoCommand.RemoveHat);
                session.PullBackdrop(rules.Backdrop);
                TickUntil(s, () => !session.Rig.SwappingBackdrop, 10f, "backdrop swap");
                while (session.Rig.Zoom < s.Camera.MaxZoom) session.ZoomBy(0.25f);
            }

            PhotoCapture shot = null;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                if (careful)
                {
                    if (!rules.ExpressionOk(subj.Expression)) subj.Command(PhotoCommand.NeutralFace);
                    subj.Command(PhotoCommand.HoldStill);
                    subj.BeginDrag();
                    subj.DragTo(0, 0);
                    TickUntil(s, () => subj.Pose == HeadPose.Center, 3f, "pose centered");
                    subj.EndDrag();
                    Tick(s, 0.3f);
                    if (Math.Abs(subj.GazeX) > 0.2f || Math.Abs(subj.GazeY) > 0.2f)
                    {
                        subj.Command(PhotoCommand.LookAtCamera);
                        Tick(s, 0.4f);
                    }
                    // Frame the head: center it, leave a little headroom.
                    var head = PortraitGeometry.HeadWorld(subj.Customer.Look, subj.Pose, subj.SeatOffset, subj.Slouch, subj.HatOn);
                    session.Rig.PanX = head.CenterX;
                    session.Rig.PanY = head.CenterY + 0.25f;
                    session.Rig.ClampTo(s.Camera);
                    if (subj.Customer.Behavior.LidDroop > 0f || subj.Expression == Expression.Tired)
                        subj.Command(PhotoCommand.OpenEyes);
                    TickUntil(s, () => subj.Lid < 0.2f && !subj.InMotion, 10f, "eyes open");
                }
                session.PressShutter();
                TickUntil(s, () => session.JustCaptured != null, 2f, "capture");
                shot = session.TakeJustCaptured();
                if (!careful) break;
                bool good = !shot.EyesClosed && !shot.EyesHalfClosed && shot.Pose == HeadPose.Center && !shot.Blurred
                    && rules.ExpressionOk(shot.Expression) && Math.Abs(shot.GazeX) < 0.3f;
                if (good) break;
            }

            if (careful)
            {
                var head = shot.HeadInFrame;
                float target = (rules.CoverageMin + rules.CoverageMax) * 0.5f;
                float cropH = head.H / target;
                float top = head.Y - 0.1f * cropH;
                shot.Crop = PortraitGeometry.MakeCrop(head.CenterX, top + cropH * 0.5f, cropH);
            }
            s.KeepPhoto(shot);
            return shot;
        }

        /// <summary>Fills a draft exactly right for its order.</summary>
        public static void FillPerfect(GameSession s, Order o, PhotoCapture photo)
        {
            var spec = s.Country(o.CountryId).Spec;
            var id = s.Customer(o.CustomerId).Identity;
            var d = o.Draft;
            d.CountryId = o.CountryId;
            d.DocType = DocType.Passport;
            d.Category = spec.Passport.Category;
            d.Cover = spec.Passport.Cover;
            d.Pages = spec.Passport.Pages;
            d.PhotoId = photo?.Id;
            foreach (var f in spec.Passport.PersonalFields)
                d.Set(f, DocumentEvaluator.Expected(f, id, spec.Passport.DateFormat));
            var issue = Calendar.DateOf(s.Day);
            d.Set(DocField.DateOfIssue, Labels.FormatDate(issue, spec.Passport.DateFormat));
            d.Set(DocField.DateOfExpiry, Labels.FormatDate(issue.AddYears(spec.Passport.ValidityYears), spec.Passport.DateFormat));
            s.GenerateDocumentNumber(o.Id);
        }

        /// <summary>Prints, waits for the tray, binds. Returns the booklet.</summary>
        public static FinishedDocument PrintAndBind(GameSession s, Order o, float max = 120f)
        {
            var page = s.PrintDraft(o.Id);
            Assert.True(page != null, "print should queue");
            TickUntil(s, () =>
            {
                if (s.Printer.Jammed) s.ClearPrinterJam();
                return s.Printer.Tray.Exists(p => p.Id == page.Id);
            }, max, "page in tray");
            var doc = s.BindPage(page.Id);
            Assert.True(doc != null, "bind should work");
            return doc;
        }
    }
}
