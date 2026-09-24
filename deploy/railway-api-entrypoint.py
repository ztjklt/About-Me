"""Give the Railway volume to the API user, then run the server without root."""

import os
import pwd
import sys


def main() -> None:
    account = pwd.getpwnam("aboutme")
    mount = "/data"
    os.makedirs(mount, exist_ok=True)
    for directory, subdirs, files in os.walk(mount):
        os.chown(directory, account.pw_uid, account.pw_gid)
        for name in subdirs + files:
            os.chown(os.path.join(directory, name), account.pw_uid, account.pw_gid)
    os.setgroups([])
    os.setgid(account.pw_gid)
    os.setuid(account.pw_uid)
    os.execvp(sys.argv[1], sys.argv[1:])


if __name__ == "__main__":
    main()
