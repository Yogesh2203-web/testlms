
from app import app, db

def initialize_database():
    with app.app_context():
        try:
            db.create_all()

            print("================================")
            print(" Institute LMS Database Setup")
            print("================================")
            print("Database connected successfully.")
            print("Tables created successfully.")

        except Exception as error:
            print("Database initialization failed.")
            print(f"Error: {error}")
            raise

if __name__ == "__main__":
    initialize_database()
