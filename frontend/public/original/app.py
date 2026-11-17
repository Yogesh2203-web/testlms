
import os
from functools import wraps

from flask import (
    Flask,
    request,
    jsonify,
    session,
    redirect,
    url_for
)

from flask_sqlalchemy import SQLAlchemy
from flask_login import (
    LoginManager,
    UserMixin,
    login_user,
    logout_user,
    login_required,
    current_user
)

from werkzeug.security import (
    generate_password_hash,
    check_password_hash
)


# -----------------------------------
# APPLICATION CONFIGURATION
# -----------------------------------

app = Flask(__name__)

app.config["SECRET_KEY"] = os.environ.get("SECRET_KEY")

if not app.config["SECRET_KEY"]:
    raise RuntimeError("Set a secure SECRET_KEY environment variable.")

database_url = os.environ.get("DATABASE_URL")

if not database_url:
    raise RuntimeError("Set the DATABASE_URL environment variable.")

# Support PostgreSQL connection URLs from hosting providers.
if database_url.startswith("postgres://"):
    database_url = database_url.replace(
        "postgres://",
        "postgresql://",
        1
    )

app.config["SQLALCHEMY_DATABASE_URI"] = database_url
app.config["SQLALCHEMY_TRACK_MODIFICATIONS"] = False
app.config["SQLALCHEMY_ENGINE_OPTIONS"] = {
    "pool_pre_ping": True
}

app.config.update(
    SESSION_COOKIE_HTTPONLY=True,
    SESSION_COOKIE_SAMESITE="Lax",
    SESSION_COOKIE_SECURE=os.environ.get(
        "COOKIE_SECURE", "true"
    ).lower() == "true",
    PERMANENT_SESSION_LIFETIME=60 * 60 * 8
)


# -----------------------------------
# DATABASE AND LOGIN INITIALIZATION
# -----------------------------------

db = SQLAlchemy(app)

login_manager = LoginManager()
login_manager.init_app(app)
login_manager.login_view = "login"


# -----------------------------------
# DATABASE MODELS
# -----------------------------------

class Branch(db.Model):
    __tablename__ = "branches"

    id = db.Column(db.Integer, primary_key=True)

    branch_name = db.Column(
        db.String(120),
        nullable=False
    )

    branch_code = db.Column(
        db.String(30),
        unique=True,
        nullable=False
    )

    address = db.Column(
        db.Text,
        nullable=True
    )

    phone = db.Column(
        db.String(20),
        nullable=True
    )

    status = db.Column(
        db.String(20),
        default="active",
        nullable=False
    )

    created_at = db.Column(
        db.DateTime,
        server_default=db.func.now()
    )


class User(UserMixin, db.Model):
    __tablename__ = "users"

    id = db.Column(db.Integer, primary_key=True)

    full_name = db.Column(
        db.String(150),
        nullable=False
    )

    email = db.Column(
        db.String(150),
        unique=True,
        nullable=False,
        index=True
    )

    password_hash = db.Column(
        db.String(255),
        nullable=False
    )

    mobile = db.Column(
        db.String(20),
        nullable=True
    )

    role = db.Column(
        db.String(30),
        nullable=False
    )

    branch_id = db.Column(
        db.Integer,
        db.ForeignKey("branches.id"),
        nullable=True
    )

    status = db.Column(
        db.String(20),
        default="active",
        nullable=False
    )

    created_at = db.Column(
        db.DateTime,
        server_default=db.func.now()
    )

    branch = db.relationship(
        "Branch",
        backref="users"
    )

    def set_password(self, password):
        self.password_hash = generate_password_hash(
            password
        )

    def verify_password(self, password):
        return check_password_hash(
            self.password_hash,
            password
        )


# -----------------------------------
# LOGIN USER LOADER
# -----------------------------------

@login_manager.user_loader
def load_user(user_id):
    try:
        return db.session.get(User, int(user_id))
    except (ValueError, TypeError):
        return None


# -----------------------------------
# ROLE PERMISSION DECORATOR
# -----------------------------------

def roles_required(*allowed_roles):

    def decorator(function):

        @wraps(function)
        @login_required
        def wrapper(*args, **kwargs):

            if current_user.role not in allowed_roles:
                return jsonify({
                    "success": False,
                    "message": "You do not have permission."
                }), 403

            if current_user.status != "active":
                logout_user()

                return jsonify({
                    "success": False,
                    "message": "Your account is inactive."
                }), 403

            return function(*args, **kwargs)

        return wrapper

    return decorator


# -----------------------------------
# BRANCH ACCESS CHECK
# -----------------------------------

def branch_access_required(branch_id):

    if current_user.role == "business_head":
        return True

    if current_user.branch_id is None:
        return False

    return current_user.branch_id == branch_id


# -----------------------------------
# HOME ROUTE
# -----------------------------------

@app.route("/")
def home():
    return jsonify({
        "success": True,
        "application": "Institute LMS",
        "message": "Backend is running."
    })


# -----------------------------------
# DATABASE HEALTH CHECK
# -----------------------------------

@app.route("/api/health")
def health():

    try:
        db.session.execute(db.text("SELECT 1"))

        return jsonify({
            "success": True,
            "database": "connected"
        })

    except Exception:
        app.logger.exception("Database health check failed")

        return jsonify({
            "success": False,
            "database": "unavailable"
        }), 503


# -----------------------------------
# LOGIN ROUTE
# -----------------------------------

@app.route("/api/login", methods=["POST"])
def login():

    if current_user.is_authenticated:
        logout_user()

    data = request.get_json(silent=True) or {}

    email = data.get("email", "").strip().lower()
    password = data.get("password", "")

    if not email or not password:
        return jsonify({
            "success": False,
            "message": "Email and password are required."
        }), 400

    user = User.query.filter_by(email=email).first()

    if not user or not user.verify_password(password):
        return jsonify({
            "success": False,
            "message": "Invalid email or password."
        }), 401

    if user.status != "active":
        return jsonify({
            "success": False,
            "message": "Your account is inactive."
        }), 403

    login_user(user, remember=False, fresh=True)

    session.permanent = True

    return jsonify({
        "success": True,
        "message": "Login successful.",
        "user": {
            "id": user.id,
            "name": user.full_name,
            "email": user.email,
            "role": user.role,
            "branch_id": user.branch_id
        }
    })


# -----------------------------------
# LOGOUT ROUTE
# -----------------------------------

@app.route("/api/logout", methods=["POST"])
@login_required
def logout():

    logout_user()

    session.clear()

    return jsonify({
        "success": True,
        "message": "Logged out successfully."
    })


# -----------------------------------
# CURRENT USER PROFILE
# -----------------------------------

@app.route("/api/me")
@login_required
def my_profile():

    if current_user.status != "active":
        logout_user()

        return jsonify({
            "success": False,
            "message": "Account inactive."
        }), 403

    return jsonify({
        "success": True,
        "user": {
            "id": current_user.id,
            "name": current_user.full_name,
            "email": current_user.email,
            "mobile": current_user.mobile,
            "role": current_user.role,
            "branch_id": current_user.branch_id
        }
    })


# -----------------------------------
# BUSINESS HEAD ONLY
# -----------------------------------

@app.route("/api/business-head/dashboard")
@roles_required("business_head")
def business_head_dashboard():

    return jsonify({
        "success": True,
        "message": "Business Head access granted."
    })


# -----------------------------------
# MANAGER ONLY
# -----------------------------------

@app.route("/api/manager/dashboard")
@roles_required("manager")
def manager_dashboard():

    return jsonify({
        "success": True,
        "message": "Manager access granted.",
        "branch_id": current_user.branch_id
    })


# -----------------------------------
# TRAINER ONLY
# -----------------------------------

@app.route("/api/trainer/dashboard")
@roles_required("trainer")
def trainer_dashboard():

    return jsonify({
        "success": True,
        "message": "Trainer access granted.",
        "branch_id": current_user.branch_id
    })


# -----------------------------------
# STUDENT ONLY
# -----------------------------------

@app.route("/api/student/dashboard")
@roles_required("student")
def student_dashboard():

    return jsonify({
        "success": True,
        "message": "Student access granted.",
        "student_id": current_user.id,
        "branch_id": current_user.branch_id
    })


# -----------------------------------
# APPLICATION ENTRY POINT
# -----------------------------------

if __name__ == "__main__":

    app.run(
        host="127.0.0.1",
        port=int(os.environ.get("PORT", 5000)),
        debug=False
    )
