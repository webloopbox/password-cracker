import random
import string
import datetime
import uuid

def generate_filename():
    """Generate a filename in the format: dictionary-2DAA54A04CB2D0E3-2025-04-19.txt"""
    hex_id = uuid.uuid4().hex[:16].upper()
    today = datetime.datetime.now().strftime("%Y-%m-%d")
    return f"dictionary-{hex_id}-{today}.txt"

def generate_common_passwords(num_passwords, output_file=None):
    """Generate num_passwords random passwords and save them to a plain text file."""
    if output_file is None:
        output_file = generate_filename()
        
    common_patterns = [
        lambda: ''.join(random.choices(string.ascii_lowercase, k=random.randint(4, 8))), 
        lambda: ''.join(random.choices(string.ascii_letters, k=random.randint(6, 10))), 
        lambda: ''.join(random.choices(string.digits, k=random.randint(4, 8))),         
        lambda: ''.join(random.choices(string.ascii_letters + string.digits, k=random.randint(6, 12))),  
        lambda: ''.join(random.choices(string.ascii_lowercase + string.digits, k=random.randint(6, 10))),  
        lambda: ''.join(random.choices(string.ascii_letters + "!@#$%^&*", k=random.randint(6, 12))) 
    ]

    print(f"Generating {num_passwords} passwords to file: {output_file}")
    with open(output_file, "w") as file:
        for _ in range(num_passwords):
            password = random.choice(common_patterns)()
            file.write(password + "\n")
    
    print(f"Dictionary generation complete: {output_file}")

if __name__ == "__main__":
    NUM_PASSWORDS = 1_000_000 
    generate_common_passwords(NUM_PASSWORDS)