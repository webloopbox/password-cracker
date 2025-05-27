import re
from datetime import datetime
from abc import ABC, abstractmethod


class ReadData(ABC):
    def __init__(self, file):
        self.file = file
        self.fmt = '%Y-%m-%d %H:%M:%S.%f'
    
    @abstractmethod
    def read_data(self):
        pass

class ReadCalculatingData(ReadData):
    def __init__(self, file):
        super().__init__(file)
        self.rgx = re.compile(r'\[INFO\]\sChecked\s(\d+)\s.*in\s(\d+)ms\sat\s\[(.*)\]')

    def read_data(self):
        data = []
        with open(self.file, 'r') as f:
            for line in f.readlines():
                if catch := re.search(self.rgx, line):
                    catch = catch.groups()
                    data.append((
                        int(catch[0]),
                        int(catch[1]),
                        datetime.strptime(catch[-1], self.fmt)))
        return data

class ReadCentralData(ReadData):
    def __init__(self, file):
        super().__init__(file)
        self.rgx = re.compile(r'\[INFO\]\s\[BruteForce\]\s.*?(\d+)\sms\s.*?Calculating:\s\((.*)\)\s.*?=\s(\d+)\sms.*=\s(\d+).*\[(.*)\]')

    def read_data(self):
        data = []
        with open(self.file, 'r') as f:
            for line in f.readlines():
                if catch := re.search(self.rgx, line):
                    catch = catch.groups()
                    data.append((
                        int(catch[0]),
                        str(catch[1]),
                        int(catch[2]),
                        int(catch[3]),
                        datetime.strptime(catch[-1], self.fmt)))
        return data
