import hashlib
import random
import string
import os

def generate_random_password():
    """Generate a random password with a random length between 1 and 34 using only letters and digits."""
    length = random.randint(1, 34)  
    chars = string.ascii_letters + string.digits
    return ''.join(random.choices(chars, k=length))

def shorten_password(password, max_length=16):
    """Shorten the password to a maximum length."""
    return password[:max_length]

def md5_hash(text):
    """Generate MD5 hash of a given text."""
    return hashlib.md5(text.encode()).hexdigest()

def generate_password_files(num_users, users_file="users_passwords.txt", plain_text_file="plain_passwords.txt"):
    """Generate user:hash pairs and save them to text files."""
    os.makedirs(os.path.dirname(users_file) or '.', exist_ok=True)
    
    with open(users_file, "w") as hash_file, open(plain_text_file, "w") as plain_file:
        for user_id in range(1, num_users + 1):
            login = f"user{user_id}"
            password = generate_random_password()
            shortened_password = shorten_password(password)
            hashed_password = md5_hash(shortened_password)
            hash_file.write(f"{login}:{hashed_password}\n")
            plain_file.write(f"{login}:{password}:{shortened_password}:{hashed_password}\n")
            if user_id % 100000 == 0:
                print(f"Generated {user_id} user records...")

if __name__ == "__main__":
    NUM_USERS = 30_000_000 
    PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
    DATA_DIR = os.path.join(PROJECT_ROOT, "data")
    os.makedirs(DATA_DIR, exist_ok=True)
    USERS_FILE = os.path.join(DATA_DIR, "users_passwords.txt")
    PLAIN_FILE = os.path.join(DATA_DIR, "plain_passwords.txt")
    print(f"Generating {NUM_USERS} user records...")
    print(f"Files will be saved to: {DATA_DIR}")
    generate_password_files(NUM_USERS, USERS_FILE, PLAIN_FILE)
    print(f"Password generation complete!")
    print(f"User:hash pairs saved to {USERS_FILE}")
    print(f"Reference data saved to {PLAIN_FILE}")