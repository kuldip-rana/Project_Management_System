using Project_Managent_System.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Project_Managent_System.Controllers
{
    public class Signup_LoginController : Controller
    {
        private readonly PMS_DatabaseEntities2 db = new PMS_DatabaseEntities2();

        // GET: Signup_Login
        public ActionResult Login()
        {
            return View();
        }

        // POST: LOGIN
        [HttpPost]
        public ActionResult Login(Main_Users u)
        {
            try
            {
                var user = db.Main_Users
                             .FirstOrDefault(x => x.Email == u.Email && x.Password == u.Password);

                if (user != null)
                {
                    Session["UserId"] = user.Id;
                    Session["Email"] = user.Email;
                    Session["Role"] = user.Role;
                    Session["Name"] = user.FirstName;

                    TempData["LoginSuccess"] = "Login successful!";

                    if (user.Role == "Admin")
                        return RedirectToAction("AdminDashboard", "Admin");

                    if (user.Role == "Manager")
                        return RedirectToAction("ManagerDashboard", "Manager");

                    if (user.Role == "User")
                        return RedirectToAction("UserDashboard", "User");

                    return RedirectToAction("Login", "Signup_Login");
                }

                ViewBag.InsertMessage = "Email or Password Incorrect";
            }
            catch (Exception ex)
            {
                // Log the exception (ex) here if needed
                ViewBag.InsertMessage = "A database error occurred. Please try again later.";
            }

            return View();
        }

        // GET SignUp Page
        public ActionResult SignUp()
        {
            return View();
        }
        // POST SignUp Page
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SignUp(Main_Users u)
        {
            if (ModelState.IsValid)
            {
                // 1. Check if the email already exists in the database
                var emailExists = db.Main_Users.Any(x => x.Email.ToLower() == u.Email.ToLower());

                if (emailExists)
                {
                    // Add a specific error to the Email field
                    ModelState.AddModelError("Email", "This email address is already registered.");
                    ViewBag.InsertMessage = "<script>alert('Email already exists. Please use a different one.')</script>";
                    return View(u);
                }

                try
                {
                    db.Main_Users.Add(u);
                    int a = db.SaveChanges();

                    if (a > 0)
                    {
                        ViewBag.InsertMessage = "<script>alert('Registered Successfully!')</script>";
                        ModelState.Clear();
                        // Optional: Redirect to login after a second
                        // return RedirectToAction("Login"); 
                        return View();
                    }
                }
                catch (Exception ex)
                {
                    ViewBag.InsertMessage = "<script>alert('An unexpected error occurred.')</script>";
                }
            }

            return View(u);
        }

        // ===============================
        // LOGOUT
        // ===============================
        public ActionResult Logout()
        {
            try
            {
                Session.Clear();
                Session.Abandon();
            }
            catch
            {
                // Silently handle logout session errors
            }

            return RedirectToAction("Login", "Signup_Login");
        }
    }
}