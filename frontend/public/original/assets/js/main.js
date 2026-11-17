
/* =====================================
   INSTITUTE LMS - MAIN JAVASCRIPT
===================================== */

document.addEventListener("DOMContentLoaded", () => {

    // 1. Smooth scrolling
    document.querySelectorAll('a[href^="#"]').forEach(link => {

        link.addEventListener("click", function (event) {

            const targetId = this.getAttribute("href");

            if (targetId === "#") return;

            const target = document.querySelector(targetId);

            if (target) {
                event.preventDefault();

                target.scrollIntoView({
                    behavior: "smooth",
                    block: "start"
                });
            }

        });

    });


    // 2. Active navigation link
    const navLinks = document.querySelectorAll(".nav-links a");

    navLinks.forEach(link => {

        link.addEventListener("click", function () {

            navLinks.forEach(item => {
                item.classList.remove("active");
            });

            this.classList.add("active");

        });

    });


    // 3. Scroll reveal animation
    const revealElements = document.querySelectorAll(
        ".course-card, .feature-card, .stat-box"
    );

    revealElements.forEach(element => {
        element.classList.add("reveal");
    });

    const observer = new IntersectionObserver((entries) => {

        entries.forEach(entry => {

            if (entry.isIntersecting) {

                entry.target.classList.add("show");

                observer.unobserve(entry.target);

            }

        });

    }, {
        threshold: 0.12
    });

    revealElements.forEach(element => {
        observer.observe(element);
    });


    // 4. Current year in footer
    const footerText = document.querySelector(".footer-bottom p");

    if (footerText) {
        footerText.textContent =
            `© ${new Date().getFullYear()} Institute LMS. All rights reserved.`;
    }


    // 5. Course button interaction
    document.querySelectorAll(".course-card a").forEach(link => {

        link.addEventListener("click", function () {

            console.log("Opening courses page");

        });

    });


    // 6. Login button interaction
    const loginButton = document.querySelector(".login-btn");

    if (loginButton) {

        loginButton.addEventListener("click", () => {
            console.log("Opening Institute LMS login portal");
        });

    }

});
