import os
import numpy as np
import pandas as pd
from enum import Enum
from datetime import datetime
import matplotlib.pyplot as plt
import matplotlib.dates as mdates
from abc import ABC, abstractmethod
from read_data import ReadCalculatingData, ReadCentralData


class CalculatingColumns(Enum):
    PACKET = 'Pakiet'
    EXECUTION_TIME = 'Czas Wykonania'
    TIMESTAMP = 'Timestamp'

class CentralColumns(Enum):
    TOTAL = 'Total'
    IP = 'Ip'
    CALCULATING = 'Obliczenia'
    COMMUNICATION = "Komunikacja"
    TIMESTAMP = 'Timestamp'

class GenerateChart(ABC):
    @abstractmethod
    def _prepare_data(self):
        pass

    @abstractmethod
    def generate(self):
        pass

class GenerateCalculatingChart(GenerateChart):
    def __init__(self, data):
        self.df = pd.DataFrame(data, columns=[col.value for col in CalculatingColumns])

    def _prepare_data(self):
        """Prepare data by calculating differences for packet sizes and execution times."""
        self.df[CalculatingColumns.PACKET.value] = (
            self.df[CalculatingColumns.PACKET.value]
            .diff()
            .fillna(self.df[CalculatingColumns.PACKET.value].iloc[0])
            .astype(int)
        )
        self.df[CalculatingColumns.EXECUTION_TIME.value] = (
            self.df[CalculatingColumns.EXECUTION_TIME.value]
            .diff()
            .fillna(self.df[CalculatingColumns.EXECUTION_TIME.value].iloc[0])
            .astype(int)
        )

    def calculate_total_time(self):
        """Calculate total execution time in milliseconds."""
        return self.df[CalculatingColumns.EXECUTION_TIME.value].sum()

    def calculate_total_size(self):
        """Calculate total packet size."""
        return self.df[CalculatingColumns.PACKET.value].sum()

    def generate(self):
        """Generate and display a chart of execution time over timestamps."""
        self._prepare_data()
        plt.figure(figsize=(10, 6))
        plt.plot(
            np.array(self.df[CalculatingColumns.TIMESTAMP.value]),
            np.array(self.df[CalculatingColumns.EXECUTION_TIME.value]),
            label='Czas Wykonania (ms)'
        )
        plt.xlabel('Timestamp')
        plt.ylabel('Czas Wykonania (ms)')
        plt.title(f"Czas wykonania dla paczek o rozmiarze {self.df[CalculatingColumns.PACKET.value].iloc[1]}")
        plt.legend(loc='best')
        plt.grid(True)
        plt.xticks(rotation=45)
        plt.tight_layout()

        text_str = (
            f"Łączny czas wykonania: {self.calculate_total_time()} ms\n"
            f"Łączny rozmiar paczek: {self.calculate_total_size()}"
        )
        plt.text(
            0.02, 0.98, text_str,
            transform=plt.gca().transAxes,
            verticalalignment='top',
            bbox=dict(boxstyle='round', facecolor='white', alpha=0.8)
        )
        plt.show()

class GenerateCentralChart(GenerateChart):
    def __init__(self, data):
        self.df = pd.DataFrame(data, columns=[col.value for col in CentralColumns])

    def _prepare_data(self):
        """Prepare data by removing rows with ip value: (average)"""
        self.df = self.df[~self.df[CentralColumns.IP.value].str.lower().eq('average')]
        self.df = self.df.reset_index(drop=True)

    def generate(self):
        """Generate and display a chart of execution time over timestamps per unique ip."""
        self._prepare_data()
        
        plt.figure(figsize=(10, 6))
        
        unique_ips = self.df[CentralColumns.IP.value].unique()
        
        for ip in unique_ips:
            ip_data = self.df[self.df[CentralColumns.IP.value] == ip]
            
            timestamps = ip_data[CentralColumns.TIMESTAMP.value].to_numpy()
            calculating = ip_data[CentralColumns.CALCULATING.value].to_numpy()
            communication = ip_data[CentralColumns.COMMUNICATION.value].to_numpy()
            
            plt.plot(timestamps, 
                     calculating, 
                     label=f'{ip} - Obliczenia', 
                     linestyle='-', 
                     marker='o')
            
            plt.plot(timestamps, 
                     communication, 
                     label=f'{ip} - Komunikacja', 
                     linestyle='--', 
                     marker='s') 

        # plt.gca().xaxis.set_major_formatter(mdates.DateFormatter('%H:%M:%S'))
        # plt.gca().xaxis.set_major_locator(mdates.AutoDateLocator(minticks=5, maxticks=8))
        # plt.gca().xaxis.set_minor_locator(mdates.MicrosecondLocator(interval=100000))


        plt.xlabel('Czas')
        plt.ylabel('Czas (ms)')
        plt.title('Czasy obliczeń i komunikacji dla każdego IP')
        plt.legend()
        plt.grid(True)
        plt.xticks(rotation=45)
        plt.tight_layout()
        plt.show()

        
def get_project_root():
    """Get the project root directory from current file location"""
    current_dir = os.path.dirname(os.path.abspath(__file__))
    return os.path.dirname(current_dir)

def find_log_files():
    """Find log files in backend central and backend calculating directories"""
    project_root = get_project_root()
    central_dir = os.path.join(project_root, "backend - central", "logs-backend-central.txt")
    calculating_dir = os.path.join(project_root, "backend - calculating", "logs-backend-calculating.txt")
    if not os.path.exists(central_dir):
        print(f"Warning: Central directory not found at {central_dir}")
    if not os.path.exists(calculating_dir):
        print(f"Warning: Calculating directory not found at {calculating_dir}")
    return central_dir, calculating_dir


if __name__ == "__main__":
    central_dir, calculating_dir = find_log_files()
    try:
        central_data = ReadCentralData(central_dir).read_data()
        calculating_data = ReadCalculatingData(calculating_dir).read_data()
        GenerateCentralChart(central_data).generate()
        GenerateCalculatingChart(calculating_data).generate()
    except TypeError as e:
        print(f"Error: Your ReadData classes may not accept directory parameters: {e}")
        print("Trying with default paths...")
        central_data = ReadCentralData().read_data()
        calculating_data = ReadCalculatingData().read_data()
        
        GenerateCentralChart(central_data).generate()
        GenerateCalculatingChart(calculating_data).generate()