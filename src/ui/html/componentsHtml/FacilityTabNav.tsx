import "../../css/components/tabs.css";

interface FacilityTabNavProps<T extends string> {
  tabs: ReadonlyArray<{ readonly id: T; readonly label: string; readonly icon?: string }>;
  activeTab: T;
  onTabChange: (tab: T) => void;
  facility: string;
}

export function FacilityTabNav<T extends string>({
  tabs,
  activeTab,
  onTabChange,
  facility,
}: FacilityTabNavProps<T>) {
  return (
    <nav className={`tab-navigation ${facility}-tabs`}>
      {tabs.map((tab) => (
        <button
          key={tab.id}
          className={`tab-item ${facility}-tab ${activeTab === tab.id ? "active" : ""}`}
          onClick={() => onTabChange(tab.id)}
        >
          {tab.icon && <span className="tab-icon">{tab.icon}</span>}
          <span className="tab-label">{tab.label}</span>
        </button>
      ))}
    </nav>
  );
}

export default FacilityTabNav;
