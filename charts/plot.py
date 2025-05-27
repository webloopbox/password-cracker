import pandas as pd
import matplotlib.pyplot as plt
import numpy as np
import seaborn as sns
from matplotlib.ticker import PercentFormatter
import os

plt.style.use('ggplot')
sns.set_palette("deep")
plt.rcParams.update({'font.size': 14})

def load_data():
    """Load data from CSV files and handle potential errors"""
    try:
        try:
            gran_df = pd.read_csv('granularity_metrics.csv')
            gran_df['Timestamp'] = gran_df['Timestamp'].str.strip()
            print(f"Loaded {len(gran_df)} granularity metrics records")
            return gran_df
        except FileNotFoundError:
            print("Granularity metrics file not found, falling back to individual files")

        dict_df = pd.DataFrame()
        try:
            dict_df = pd.read_csv('dictionary_chunk_metrics.csv')
            dict_df['Timestamp'] = dict_df['Timestamp'].str.strip()
            dict_df['PasswordFound'] = dict_df['PasswordFound'].astype(bool)
            dict_df['Granularity'] = pd.to_numeric(dict_df['Granularity'], errors='coerce')
            dict_df['ChunkSize'] = pd.to_numeric(dict_df['ChunkSize'], errors='coerce')
            if 'ChunkEnd' in dict_df.columns and 'ChunkStart' in dict_df.columns:
                dict_df.loc[dict_df['ChunkSize'] == 0, 'ChunkSize'] = dict_df['ChunkEnd'] - dict_df['ChunkStart'] + 1
            dict_df['MethodType'] = 'dictionary'
            
            print(f"Loaded {len(dict_df)} dictionary chunk records with granularities: {dict_df['Granularity'].unique()}")
        except FileNotFoundError:
            print("Dictionary metrics file not found")
        bf_pkg_df = pd.DataFrame()
        try:
            bf_pkg_df = pd.read_csv('bruteforce_package_metrics.csv')
            bf_pkg_df['Timestamp'] = bf_pkg_df['Timestamp'].str.strip()
            bf_pkg_df['PasswordFound'] = bf_pkg_df['PasswordFound'].astype(bool)
            bf_pkg_df['ProcessingTime'] = bf_pkg_df['ProcessingTime']
            bf_pkg_df['TotalTime'] = bf_pkg_df['TotalTime']
            bf_pkg_df['ChunkSize'] = bf_pkg_df['CharPackage'].apply(len)
            bf_pkg_df['Granularity'] = pd.to_numeric(bf_pkg_df['Granularity'], errors='coerce')
            bf_pkg_df['MethodType'] = 'bruteforce'
            
            print(f"Loaded {len(bf_pkg_df)} brute force package records with granularities: {bf_pkg_df['Granularity'].unique()}")
        except FileNotFoundError:
            print("Brute force package metrics file not found")
            
        bf_df = pd.DataFrame()
        if bf_pkg_df.empty:
            try:
                bf_df = pd.read_csv('bruteforce_metrics.csv')
                bf_df['Timestamp'] = bf_df['Timestamp'].str.strip()
                bf_df['PasswordFound'] = bf_df['PasswordFound'].astype(bool)
                bf_df['Granularity'] = pd.to_numeric(bf_df['Granularity'], errors='coerce')
                bf_df['MethodType'] = 'bruteforce'
                
                print(f"Loaded {len(bf_df)} brute force summary records with granularities: {bf_df['Granularity'].unique()}")
            except FileNotFoundError:
                print("No brute force metrics files found")
        else:
            bf_df = bf_pkg_df
        if not dict_df.empty and not bf_df.empty:
            combined_df = pd.concat([dict_df, bf_df], ignore_index=True)
            return combined_df
        elif not dict_df.empty:
            return dict_df
        elif not bf_df.empty:
            return bf_df
        else:
            return pd.DataFrame()
            
    except Exception as e:
        print(f"Error loading data: {e}")
        import traceback
        traceback.print_exc()
        return pd.DataFrame()

def calculate_efficiency_metrics(df):
    """Calculate efficiency metrics and group by granularity"""
    if df.empty:
        print("No data to calculate metrics")
        return pd.DataFrame()
    
    result_dfs = []
    df['CommunicationOverhead'] = df['TotalTime'] - df['ProcessingTime']
    df['Efficiency'] = df['ProcessingTime'] / df['TotalTime']
    for method in df['MethodType'].unique():
        method_df = df[df['MethodType'] == method]
        group_col = 'Granularity'
        grouped = method_df.groupby(group_col).agg({
            'Efficiency': 'mean',
            'ProcessingTime': 'mean',
            'TotalTime': 'mean',
            'CommunicationOverhead': 'mean',
            'ChunkSize': 'mean' if 'ChunkSize' in method_df.columns else lambda x: None,
            'MethodType': 'first'
        }).reset_index()
        
        result_dfs.append(grouped)
        print(f"\n{method.title()} efficiency metrics grouped by {group_col}:")
        cols_to_print = [col for col in [group_col, 'ChunkSize', 'Efficiency', 'ProcessingTime', 'TotalTime'] 
                         if col in grouped.columns and not grouped[col].isna().all()]
        print(grouped[cols_to_print])
    
    if result_dfs:
        return pd.concat(result_dfs, ignore_index=True)
    return pd.DataFrame()

def plot_granularity_efficiency(metrics_df):
    """Create granularity vs efficiency plot"""
    plt.figure(figsize=(12, 8))
    
    if metrics_df.empty:
        print("No data available for plotting")
        return
    dict_data = metrics_df[metrics_df['MethodType'] == 'dictionary']
    if not dict_data.empty:
        dict_data_sorted = dict_data.sort_values('Granularity')
        plt.plot(dict_data_sorted['Granularity'], dict_data_sorted['Efficiency'], 
                marker='o', linestyle='-', linewidth=2, markersize=10,
                label='Dictionary Cracking')
        for i, row in dict_data_sorted.iterrows():
            plt.annotate(f"{int(row['Granularity']):,}", 
                        (row['Granularity'], row['Efficiency']),
                        textcoords="offset points", 
                        xytext=(0,10), 
                        ha='center')
    bf_data = metrics_df[metrics_df['MethodType'] == 'bruteforce']
    if not bf_data.empty:
        bf_data_sorted = bf_data.sort_values('Granularity')
        plt.plot(bf_data_sorted['Granularity'], bf_data_sorted['Efficiency'], 
                marker='s', linestyle='--', linewidth=2, markersize=10,
                color='#d95f02', label='Brute Force Cracking')
        for i, row in bf_data_sorted.iterrows():
            plt.annotate(f"BF: {int(row['Granularity'])}", 
                        (row['Granularity'], row['Efficiency']),
                        textcoords="offset points", 
                        xytext=(0,10), 
                        ha='center')
    x = np.linspace(1, 100000, 1000)
    y = 0.95 * (1 - np.exp(-x/1000)) * (np.exp(-x/50000) + 0.2)
    plt.plot(x, y, 'k--', alpha=0.3, label='Theoretical Curve')
    
    plt.xlabel('Granularity (Chunk/Package Size)', fontsize=14)
    plt.ylabel('Efficiency (Processing Time / Total Time)', fontsize=14)
    plt.title('Efficiency vs. Granularity', fontsize=16)
    plt.grid(True)
    plt.legend(fontsize=12)
    plt.xscale('log')  
    plt.gca().yaxis.set_major_formatter(PercentFormatter(1.0))
    plt.tight_layout()
    plt.savefig('granularity_efficiency_plot.png', dpi=300)
    print("\nSaved granularity efficiency plot to 'granularity_efficiency_plot.png'")
    plt.show()

def main():
    print("=== Granularity and Efficiency Analysis ===")
    data = load_data()
    metrics_df = calculate_efficiency_metrics(data)
    plot_granularity_efficiency(metrics_df)
    
    print("\nAnalysis complete!")

if __name__ == "__main__":
    main()