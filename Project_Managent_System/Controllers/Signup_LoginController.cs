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

        // POST METHOD SIGNUP
        [HttpPost]
        public ActionResult SignUp(Main_Users u)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    db.Main_Users.Add(u);
                    int a = db.SaveChanges();

                    if (a > 0)
                    {
                        ViewBag.InsertMessage = "<script>alert('Registered Successfully')</script>";
                        ModelState.Clear(); // Clear the form after successful registration
                    }
                    else
                    {
                        ViewBag.InsertMessage = "<script>alert('Registration Failed')</script>";
                    }
                }
            }
            catch (Exception ex)
            {
                // Common error: Duplicate Email
                if (ex.InnerException != null && ex.InnerException.InnerException != null &&
                    ex.InnerException.InnerException.Message.Contains("Unique Constraint"))
                {
                    ViewBag.InsertMessage = "<script>alert('Email already exists.')</script>";
                }
                else
                {
                    ViewBag.InsertMessage = "<script>alert('An error occurred during registration.')</script>";
                }
            }

            return View();
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