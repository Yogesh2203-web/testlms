
from getpass import getpass
from sqlalchemy import select
from app import app, db, User


def create_business_head():
    print("\n================================")
    print(" Create Business Head Account")
    print("================================")

    full_name = input("Full name: ").strip()
    email = input("Email address: ").strip().lower()
    mobile = input("Mobile number (optional): ").strip()

    if not full_name or not email:
        print("Name and email are required.")
        return

    password = getpass("Create password: ")
    confirm_password = getpass("Confirm password: ")

    if len(password) < 12:
        print("Password must contain at least 12 characters.")
        return

    if password != confirm_password:
        print("Passwords do not match.")
        return

    with app.app_context():
        try:
            existing_user = db.session.scalar(
                select(User).where(User.email == email)
            )

            if existing_user:
                print("An account with this email already exists.")
                return

            admin = User(
                full_name=full_name,
                email=email,
                mobile=mobile or None,
                role="business_head",
                branch_id=None,
                status="active"
            )

            admin.set_password(password)

            db.session.add(admin)
            db.session.commit()

            print("\nBusiness Head account created successfully.")
            print(f"Name: {admin.full_name}")
            print(f"Email: {admin.email}")
            print("Role: Business Head")
            print("Password: Hidden")

        except Exception as error:
            db.session.rollback()
            print(f"\nAccount creation failed: {error}")


if __name__ == "__main__":
    create_business_head()
