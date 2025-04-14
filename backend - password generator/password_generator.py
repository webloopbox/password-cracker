import hashlib
import random
import string

def generate_random_password():
    """Generate a random password with a random length between 1 and 34 using only letters and digits."""
    length = random.randint(1, 34)  
    chars = string.ascii_letters + string.digits
    return ''.join(random.choices(chars, k=length))

def md5_hash(text):
    """Generate MD5 hash of a given text."""
    return hashlib.md5(text.encode()).hexdigest()

def generate_sql_and_plain_text(num_hashes, sql_output_file="insert_users.sql", plain_text_file="plain_passwords.txt"):
    """Generate num_hashes MD5 hashes and save them to SQL and plain text files."""
    with open(sql_output_file, "w") as sql_file, open(plain_text_file, "w") as plain_file:
        sql_file.write("""
        CREATE TABLE IF NOT EXISTS users (
            id INT PRIMARY KEY,
            login VARCHAR(255) NOT NULL,
            password VARCHAR(255) NOT NULL
        );
        \n""")
        for user_id in range(1, num_hashes + 1):
            login = f"user{user_id}"
            password = generate_random_password()
            hashed_password = md5_hash(password)
            sql_file.write(f"INSERT INTO users (id, login, password) VALUES ({user_id}, '{login}', '{hashed_password}');\n")
            plain_file.write(f"{user_id}: {password}\n")

if __name__ == "__main__":
    NUM_HASHES = 30_000_000  
    generate_sql_and_plain_text(NUM_HASHES)